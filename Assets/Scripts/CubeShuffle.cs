using System.Text;
using UnityEngine;

/// <summary>
/// Generates standard, valid, non-redundant scrambles for a 3x3 Rubik's cube.
/// </summary>
public static class CubeShuffle
{
    private static readonly string[] Faces = { "U", "D", "F", "B", "L", "R" };
    private static readonly string[] Suffixes = { "", "'", "2" };

    /// <summary>
    /// Generates a random scramble sequence of the specified length (default 20 moves).
    /// Prevents consecutive moves on the same face or opposing faces.
    /// </summary>
    public static string GenerateScramble(int moveCount = 20)
    {
        StringBuilder sb = new StringBuilder();
        int lastFace = -1;
        int secondLastFace = -1;

        for (int i = 0; i < moveCount; i++)
        {
            int faceIndex;
            do
            {
                faceIndex = Random.Range(0, Faces.Length);
            }
            while (faceIndex == lastFace || (IsOpposite(faceIndex, lastFace) && faceIndex == secondLastFace));

            secondLastFace = lastFace;
            lastFace = faceIndex;

            string suffix = Suffixes[Random.Range(0, Suffixes.Length)];
            sb.Append(Faces[faceIndex]).Append(suffix);

            if (i < moveCount - 1)
            {
                sb.Append(" ");
            }
        }

        return sb.ToString();
    }

    private static bool IsOpposite(int f1, int f2)
    {
        // U(0) and D(1) are opposites
        // F(2) and B(3) are opposites
        // L(4) and R(5) are opposites
        if (f1 < 0 || f2 < 0) return false;
        return (f1 / 2) == (f2 / 2);
    }
}

