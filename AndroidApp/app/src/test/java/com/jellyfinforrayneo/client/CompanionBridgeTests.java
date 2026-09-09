package com.jellyfinforrayneo.client;

import android.webkit.JavascriptInterface;
import java.lang.reflect.Method;
import org.junit.Test;
import static org.junit.Assert.assertNotNull;

public final class CompanionBridgeTests
{
    @Test
    public void declaredBridgeMethods_RemainExposedOnTheConcreteWebViewBridge() throws Exception
    {
        Class<?> implementation = Class.forName("com.jellyfinforrayneo.client.MainActivity$CompanionBridge");
        for (Method contract : CompanionWebViewController.JavascriptBridge.class.getDeclaredMethods())
        {
            Method method = implementation.getDeclaredMethod(contract.getName(), contract.getParameterTypes());
            assertNotNull(contract.getName(), method.getAnnotation(JavascriptInterface.class));
        }
    }
}
