package com.jellyfinforrayneo.client;

import java.util.ArrayDeque;
import java.util.Locale;

final class RemoteCommandRouter
{
    static final int MAX_PENDING_COMMANDS = 32;

    interface CommandSink
    {
        boolean dispatch(String command);
    }

    private final ArrayDeque<String> pending = new ArrayDeque<>();
    private CommandSink sink;
    private boolean ready;

    synchronized void setSink(CommandSink sink)
    {
        this.sink = sink;
        flush();
    }

    synchronized void setReady(boolean ready)
    {
        this.ready = ready;
        if (ready)
        {
            flush();
        }
    }

    synchronized boolean submit(String value)
    {
        String normalized = normalize(value);
        if (normalized == null)
        {
            return false;
        }
        if (ready && sink != null && sink.dispatch(normalized))
        {
            return true;
        }
        enqueue(normalized);
        return true;
    }

    synchronized boolean submitVolume(int percentage)
    {
        int bounded = Math.max(0, Math.min(100, percentage));
        String command = "volume:" + bounded;
        if (ready && sink != null && sink.dispatch(command))
        {
            return true;
        }
        enqueue(command);
        return true;
    }

    synchronized int pendingCount()
    {
        return pending.size();
    }

    synchronized void clear()
    {
        pending.clear();
    }

    private void enqueue(String command)
    {
        while (pending.size() >= MAX_PENDING_COMMANDS)
        {
            pending.removeFirst();
        }
        pending.addLast(command);
    }

    private void flush()
    {
        if (!ready || sink == null)
        {
            return;
        }
        while (!pending.isEmpty())
        {
            String command = pending.peekFirst();
            if (!sink.dispatch(command))
            {
                return;
            }
            pending.removeFirst();
        }
    }

    private static String normalize(String value)
    {
        String command = value == null ? "" : value.trim().toLowerCase(Locale.US);
        switch (command)
        {
            case "up":
            case "down":
            case "left":
            case "right":
            case "back":
                return command;
            case "submit":
            case "enter":
                return "enter";
            default:
                return null;
        }
    }
}
