package com.jellyfinforrayneo.client;

import java.util.List;
import java.util.Locale;

final class DisplaySelector
{
    static final class Candidate
    {
        final int id;
        final boolean valid;
        final boolean on;
        final boolean presentation;
        final String name;

        Candidate(int id, boolean valid, boolean on, boolean presentation, String name)
        {
            this.id = id;
            this.valid = valid;
            this.on = on;
            this.presentation = presentation;
            this.name = name == null ? "" : name;
        }
    }

    private DisplaySelector()
    {
    }

    static int selectBestIndex(List<Candidate> candidates, int defaultDisplayId)
    {
        int bestIndex = -1;
        int bestScore = Integer.MIN_VALUE;
        for (int index = 0; index < candidates.size(); index++)
        {
            Candidate candidate = candidates.get(index);
            if (candidate == null
                    || !candidate.valid
                    || !candidate.on
                    || candidate.id == defaultDisplayId)
            {
                continue;
            }

            int score = candidate.presentation ? 100 : 0;
            String name = candidate.name.toLowerCase(Locale.ROOT);
            if (name.contains("smartglasses")
                    || name.contains("rayneo")
                    || name.contains("tcl")
                    || name.contains("hdmi"))
            {
                score += 200;
            }
            if (bestIndex < 0 || score > bestScore)
            {
                bestIndex = index;
                bestScore = score;
            }
        }
        return bestIndex;
    }
}
