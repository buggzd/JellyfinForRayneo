package com.jellyfinforrayneo.client;

import org.junit.Test;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public final class DiagnosticLogTests
{
    @Test
    public void exportEvents_ContainsOnlyFixedEventNames()
    {
        DiagnosticLog log = new DiagnosticLog();

        log.record(DiagnosticLog.Event.APP_CREATED);
        log.record(DiagnosticLog.Event.RUNTIME_ERROR_NETWORK);

        String report = log.exportEvents();
        assertTrue(report.contains("APP_CREATED"));
        assertTrue(report.contains("RUNTIME_ERROR_NETWORK"));
        assertFalse(report.contains("http://"));
        assertFalse(report.contains("token"));
    }

    @Test
    public void record_DropsOldestEventsAtFixedCapacity()
    {
        DiagnosticLog log = new DiagnosticLog();
        log.record(DiagnosticLog.Event.APP_CREATED);
        for (int index = 0; index < DiagnosticLog.MAX_EVENTS; index++)
        {
            log.record(DiagnosticLog.Event.RUNTIME_LOADING);
        }

        String report = log.exportEvents();
        assertFalse(report.contains("APP_CREATED"));
        assertTrue(report.contains("RUNTIME_LOADING"));
    }
}
