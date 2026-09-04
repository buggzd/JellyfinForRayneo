package com.jellyfinforrayneo.client;

import android.content.Context;
import android.net.wifi.WifiManager;

import org.json.JSONObject;

import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.net.InterfaceAddress;
import java.net.NetworkInterface;
import java.net.SocketTimeoutException;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Enumeration;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

final class JellyfinDiscoveryService
{
    private static final int PORT = 7_359;
    private static final int DURATION_MS = 3_000;
    private static final byte[] MESSAGE = "who is JellyfinServer?"
            .getBytes(StandardCharsets.UTF_8);

    static final class Server
    {
        final String name;
        final String address;
        final String id;

        Server(String name, String address, String id)
        {
            this.name = bounded(name, SessionPayload.MAX_SERVER_NAME_LENGTH);
            this.address = bounded(address, SessionPayload.MAX_SERVER_URL_LENGTH);
            this.id = bounded(id, SessionPayload.MAX_IDENTIFIER_LENGTH);
        }
    }

    interface Callback
    {
        void onComplete(int generation, List<Server> servers, boolean failed);
    }

    private final Context context;
    private final Callback callback;
    private final ExecutorService executor = Executors.newSingleThreadExecutor(runnable ->
    {
        Thread thread = new Thread(runnable, "Jellyfin-Discovery");
        thread.setDaemon(true);
        return thread;
    });
    private volatile int generation;
    private volatile DatagramSocket activeSocket;

    JellyfinDiscoveryService(Context context, Callback callback)
    {
        this.context = context.getApplicationContext();
        this.callback = callback;
    }

    int scan()
    {
        cancel();
        int operation = generation;
        executor.execute(() -> scan(operation));
        return operation;
    }

    void cancel()
    {
        generation++;
        DatagramSocket socket = activeSocket;
        activeSocket = null;
        if (socket != null)
        {
            socket.close();
        }
    }

    boolean isCurrent(int operation)
    {
        return operation == generation;
    }

    void close()
    {
        cancel();
        executor.shutdownNow();
    }

    private void scan(int operation)
    {
        Map<String, Server> found = new LinkedHashMap<>();
        DatagramSocket socket = null;
        WifiManager.MulticastLock multicastLock = null;
        boolean failed = false;
        try
        {
            WifiManager wifiManager = (WifiManager) context.getSystemService(Context.WIFI_SERVICE);
            if (wifiManager != null)
            {
                multicastLock = wifiManager.createMulticastLock("jellyfin-rayneo-discovery");
                multicastLock.setReferenceCounted(false);
                multicastLock.acquire();
            }

            socket = new DatagramSocket();
            socket.setBroadcast(true);
            socket.setSoTimeout(350);
            if (operation != generation)
            {
                return;
            }
            activeSocket = socket;

            for (InetAddress broadcast : broadcastAddresses())
            {
                try
                {
                    socket.send(new DatagramPacket(MESSAGE, MESSAGE.length, broadcast, PORT));
                }
                catch (Exception ignored)
                {
                    // Continue with other interfaces.
                }
            }

            long deadline = System.currentTimeMillis() + DURATION_MS;
            byte[] buffer = new byte[4_096];
            while (operation == generation && System.currentTimeMillis() < deadline)
            {
                DatagramPacket response = new DatagramPacket(buffer, buffer.length);
                try
                {
                    socket.receive(response);
                    Server server = parse(response);
                    if (server != null)
                    {
                        String key = server.id.isEmpty()
                                ? server.address.toLowerCase(Locale.ROOT)
                                : server.id;
                        found.put(key, server);
                    }
                }
                catch (SocketTimeoutException ignored)
                {
                    // Poll cancellation and deadline.
                }
            }
        }
        catch (Exception ignored)
        {
            failed = true;
        }
        finally
        {
            if (activeSocket == socket)
            {
                activeSocket = null;
            }
            if (socket != null)
            {
                socket.close();
            }
            if (multicastLock != null && multicastLock.isHeld())
            {
                multicastLock.release();
            }
        }

        if (operation == generation)
        {
            callback.onComplete(operation, new ArrayList<>(found.values()), failed);
        }
    }

    private static List<InetAddress> broadcastAddresses()
    {
        Map<String, InetAddress> result = new LinkedHashMap<>();
        try
        {
            InetAddress global = InetAddress.getByName("255.255.255.255");
            result.put(global.getHostAddress(), global);
        }
        catch (Exception ignored)
        {
        }
        try
        {
            Enumeration<NetworkInterface> interfaces = NetworkInterface.getNetworkInterfaces();
            while (interfaces != null && interfaces.hasMoreElements())
            {
                NetworkInterface network = interfaces.nextElement();
                if (!network.isUp() || network.isLoopback())
                {
                    continue;
                }
                for (InterfaceAddress address : network.getInterfaceAddresses())
                {
                    InetAddress broadcast = address.getBroadcast();
                    if (broadcast != null)
                    {
                        result.put(broadcast.getHostAddress(), broadcast);
                    }
                }
            }
        }
        catch (Exception ignored)
        {
        }
        return new ArrayList<>(result.values());
    }

    private static Server parse(DatagramPacket packet)
    {
        try
        {
            String text = new String(
                    packet.getData(),
                    packet.getOffset(),
                    packet.getLength(),
                    StandardCharsets.UTF_8).trim();
            JSONObject source = new JSONObject(text);
            String address = firstNonEmpty(
                    source.optString("Address", ""),
                    source.optString("address", ""));
            address = sanitizeAddress(address, packet.getAddress());
            if (address == null)
            {
                return null;
            }
            String name = firstNonEmpty(
                    source.optString("Name", ""),
                    source.optString("name", ""));
            if (name == null || name.trim().isEmpty())
            {
                name = packet.getAddress().getHostAddress();
            }
            String id = firstNonEmpty(
                    source.optString("Id", ""),
                    source.optString("id", ""));
            return new Server(name, address, id);
        }
        catch (Exception ignored)
        {
            return null;
        }
    }

    static String sanitizeAddress(String value, InetAddress source)
    {
        if (value == null || value.length() > SessionPayload.MAX_SERVER_URL_LENGTH)
        {
            return null;
        }
        String candidate = value.trim();
        if (!candidate.regionMatches(true, 0, "http://", 0, 7)
                && !candidate.regionMatches(true, 0, "https://", 0, 8))
        {
            return null;
        }
        try
        {
            java.net.URI uri = new java.net.URI(candidate);
            String host = uri.getHost();
            if (uri.getUserInfo() != null
                    || uri.getRawQuery() != null
                    || uri.getRawFragment() != null)
            {
                return null;
            }
            if (host == null || host.isEmpty() || "0.0.0.0".equals(host) || "::".equals(host))
            {
                host = source.getHostAddress();
                if (host.contains(":"))
                {
                    host = "[" + host + "]";
                }
                StringBuilder rebuilt = new StringBuilder();
                rebuilt.append(uri.getScheme()).append("://").append(host);
                if (uri.getPort() >= 0)
                {
                    rebuilt.append(":").append(uri.getPort());
                }
                if (uri.getRawPath() != null && !"/".equals(uri.getRawPath()))
                {
                    rebuilt.append(uri.getRawPath());
                }
                candidate = rebuilt.toString();
            }
            return SessionPayload.normalizeServerUrl(candidate);
        }
        catch (Exception ignored)
        {
            return null;
        }
    }

    private static String firstNonEmpty(String first, String second)
    {
        return first == null || first.trim().isEmpty() ? second : first;
    }

    private static String bounded(String value, int maximumLength)
    {
        String normalized = value == null ? "" : value.trim();
        return normalized.length() <= maximumLength
                ? normalized
                : normalized.substring(0, maximumLength);
    }
}
