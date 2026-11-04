using System.Collections.Generic;
using UnityEngine;

namespace Training
{
    public static class ArenaPlanner
    {
        public static List<Rect> Plan(Rect tileBounds, float arenaSize, int count, System.Random rng)
        {
            float width = Mathf.Min(arenaSize, tileBounds.width);
            float height = Mathf.Min(arenaSize, tileBounds.height);
            int columns = Mathf.Max(1, Mathf.FloorToInt(tileBounds.width / width));
            int rows = Mathf.Max(1, Mathf.FloorToInt(tileBounds.height / height));

            var cells = new List<int>(columns * rows);
            for (int i = 0; i < columns * rows; i++)
                cells.Add(i);

            for (int i = cells.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int swap = cells[i];
                cells[i] = cells[j];
                cells[j] = swap;
            }

            float slackX = (tileBounds.width - columns * width) / columns;
            float slackZ = (tileBounds.height - rows * height) / rows;

            var arenas = new List<Rect>(count);
            for (int i = 0; i < Mathf.Min(count, cells.Count); i++)
            {
                int column = cells[i] % columns;
                int row = cells[i] / columns;
                float x = tileBounds.xMin + column * (width + slackX) + (float)rng.NextDouble() * slackX;
                float z = tileBounds.yMin + row * (height + slackZ) + (float)rng.NextDouble() * slackZ;
                arenas.Add(new Rect(x, z, width, height));
            }

            return arenas;
        }

        public static Rect Bounds(List<Rect> arenas)
        {
            Rect bounds = arenas[0];
            foreach (Rect arena in arenas)
            {
                bounds = Rect.MinMaxRect(
                    Mathf.Min(bounds.xMin, arena.xMin), Mathf.Min(bounds.yMin, arena.yMin),
                    Mathf.Max(bounds.xMax, arena.xMax), Mathf.Max(bounds.yMax, arena.yMax));
            }

            return bounds;
        }
    }
}
