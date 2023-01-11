﻿using UnityEngine;

namespace FactionBlender;

public static class Extensions
{
    public static bool Contains(this Rect rect, Rect otherRect)
    {
        if (!rect.Contains(new Vector2(otherRect.xMin, otherRect.yMin)))
        {
            return false;
        }

        if (!rect.Contains(new Vector2(otherRect.xMin, otherRect.yMax)))
        {
            return false;
        }

        if (!rect.Contains(new Vector2(otherRect.xMax, otherRect.yMax)))
        {
            return false;
        }

        if (!rect.Contains(new Vector2(otherRect.xMax, otherRect.yMin)))
        {
            return false;
        }

        return true;
    }
}