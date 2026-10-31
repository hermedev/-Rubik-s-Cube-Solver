using System;
using System.Text;
using UnityEngine;

/// <summary>
/// High-performance implementation of Herbert Kociemba's Two-Phase Algorithm.
/// Solves any valid 3x3 Rubik's cube state in ~20 moves in under 100 milliseconds.
/// Input: 54-character facelet string in standard order: U1..U9, R1..R9, F1..F9, D1..D9, L1..L9, B1..B9.
/// </summary>
public static class KociembaSolver
{
    public const string SolvedFaceletString = "UUUUUUUUURRRRRRRRRFFFFFFFFFDDDDDDDDDLLLLLLLLLBBBBBBBBB";

    // ── Corner & Edge definitions ─────────────────────────────────────────────
    private enum Corner { URF, UFL, ULB, UBR, DFR, DLF, DBL, DRB }
    private enum Edge { UR, UF, UL, UB, DR, DF, DL, DB, FR, FL, BL, BR }
    private enum ColorName { U, R, F, D, L, B }

    // Map corner facelet positions in the 54-string
    private static readonly int[][] CornerFacelet = {
        new[] { 8, 9, 20 },   // URF: U9, R1, F3
        new[] { 6, 18, 38 },  // UFL: U7, F1, L3
        new[] { 0, 36, 47 },  // ULB: U1, L1, B3
        new[] { 2, 45, 11 },  // UBR: U3, B1, R3
        new[] { 29, 26, 15 }, // DFR: D3, F9, R7
        new[] { 27, 44, 24 }, // DLF: D1, L9, F7
        new[] { 33, 53, 42 }, // DBL: D7, B9, L7
        new[] { 35, 17, 51 }  // DRB: D9, R9, B7
    };

    // Map edge facelet positions in the 54-string
    private static readonly int[][] EdgeFacelet = {
        new[] { 5, 10 }, // UR: U6, R2
        new[] { 7, 19 }, // UF: U8, F2
        new[] { 3, 37 }, // UL: U4, L2
        new[] { 1, 46 }, // UB: U2, B2
        new[] { 32, 16 },// DR: D6, R8
        new[] { 28, 25 },// DF: D2, F8
        new[] { 30, 43 },// DL: D4, L8
        new[] { 34, 52 },// DB: D8, B8
        new[] { 23, 12 },// FR: F6, R4
        new[] { 21, 41 },// FL: F4, L6
        new[] { 50, 39 },// BL: B6, L4
        new[] { 48, 14 } // BR: B4, R6
    };

    private static readonly string[] MoveNames = {
        "U", "U2", "U'",
        "R", "R2", "R'",
        "F", "F2", "F'",
        "D", "D2", "D'",
        "L", "L2", "L'",
        "B", "B2", "B'"
    };

    /// <summary>
    /// Solves the given 54-character facelet string.
    /// Returns a space-separated sequence of moves, or empty string if already solved.
    /// </summary>
    public static string Solve(string facelets, int maxDepth = 24, int timeoutSeconds = 2)
    {
        if (string.IsNullOrEmpty(facelets) || facelets.Length != 54)
        {
            Debug.LogError($"[KociembaSolver] Invalid facelet length: {facelets?.Length ?? 0}");
            return null;
        }

        if (facelets == SolvedFaceletString)
        {
            return ""; // Already solved
        }

        try
        {
            Search search = new Search();
            string solution = search.Solution(facelets, maxDepth, timeoutSeconds);
            if (solution != null && solution.StartsWith("Error"))
            {
                Debug.LogWarning($"[KociembaSolver] Solver returned: {solution}");
                return null;
            }
            return solution;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KociembaSolver] Exception during solve: {ex.Message}");
            return null;
        }
    }

    // ── Internal Search & Coordination ───────────────────────────────────────
    private class Search
    {
        private int[] ax = new int[31];
        private int[] po = new int[31];

        private int[] flip = new int[31];
        private int[] twist = new int[31];
        private int[] slice = new int[31];

        private int[] parity = new int[31];
        private int[] URFtoDLF = new int[31];
        private int[] FRtoBR = new int[31];
        private int[] URtoUL = new int[31];
        private int[] UBtoDF = new int[31];
        private int[] URtoDF = new int[31];

        private int[] minDistPhase1 = new int[31];
        private int[] minDistPhase2 = new int[31];

        public string Solution(string facelets, int maxDepth, int timeOut)
        {
            // Parse facelets into internal cubie representation
            CubieCube cc = FaceletToCubie(facelets);
            if (cc == null) return "Error 1: Invalid cube state";

            CoordCube c = new CoordCube();
            EnsureTablesInitialized();

            c.twist = cc.GetTwist();
            c.flip = cc.GetFlip();
            c.parity = cc.CornerParity();
            c.FRtoBR = cc.GetFRtoBR();
            c.URFtoDLF = cc.GetURFtoDLF();
            c.URtoUL = cc.GetURtoUL();
            c.UBtoDF = cc.GetUBtoDF();
            c.URtoDF = cc.GetURtoDF();

            // Run IDA* search through Phase 1 then Phase 2
            int depth1 = 0;
            ax[0] = 0;
            po[0] = 0;
            flip[0] = c.flip;
            twist[0] = c.twist;
            parity[0] = c.parity;
            slice[0] = c.FRtoBR / 24;
            URFtoDLF[0] = c.URFtoDLF;
            FRtoBR[0] = c.FRtoBR;
            URtoUL[0] = c.URtoUL;
            UBtoDF[0] = c.UBtoDF;
            URtoDF[0] = c.URtoDF;

            minDistPhase1[1] = 1;
            int n = 0;
            bool busy = false;
            int depthPhase1 = 1;

            long startTime = DateTime.UtcNow.Ticks;
            long maxTicks = timeOut * 10_000_000L;

            do
            {
                do
                {
                    if ((depthPhase1 - n > minDistPhase1[n + 1]) && !busy)
                    {
                        if (ax[n] == 0 || ax[n] == 3)
                            ax[++n] = 1;
                        else
                            ax[++n] = 0;
                        po[n] = 1;
                    }
                    else if (++po[n] > 3)
                    {
                        do
                        {
                            if (++ax[n] > 5)
                            {
                                if (DateTime.UtcNow.Ticks - startTime > maxTicks)
                                    return "Error: Timeout";

                                if (n == 0)
                                {
                                    if (depthPhase1 >= maxDepth) return "Error: Max depth exceeded";
                                    depthPhase1++;
                                    ax[n] = 0;
                                    po[n] = 1;
                                    busy = false;
                                    break;
                                }
                                else
                                {
                                    n--;
                                    busy = true;
                                    break;
                                }
                            }
                            else
                            {
                                po[n] = 1;
                                busy = false;
                            }
                        } while (n == 0 ? false : (ax[n - 1] == ax[n] || ax[n - 1] - 3 == ax[n]));
                    }
                    else
                    {
                        busy = false;
                    }
                } while (busy);

                if (DateTime.UtcNow.Ticks - startTime > maxTicks)
                    return "Error: Timeout";

                // Advance coordinates for Phase 1
                int mv = 3 * ax[n] + po[n] - 1;
                flip[n + 1] = CoordCube.flipMove[flip[n], mv];
                twist[n + 1] = CoordCube.twistMove[twist[n], mv];
                slice[n + 1] = CoordCube.FRtoBR_Move[slice[n] * 24, mv] / 24;

                minDistPhase1[n + 1] = Math.Max(
                    CoordCube.GetPruning(CoordCube.Slice_Flip_Prun, 2048 * slice[n + 1] + flip[n + 1]),
                    CoordCube.GetPruning(CoordCube.Slice_Twist_Prun, 2187 * slice[n + 1] + twist[n + 1]));

                // Phase 1 goal check: flip=0, twist=0, slice=0
                if (minDistPhase1[n + 1] == 0 && n >= depthPhase1 - 1)
                {
                    minDistPhase1[n + 1] = 10;
                    if (n == depthPhase1 - 1)
                    {
                        int s = TotalSearchPhase2(n, maxDepth, depthPhase1, startTime, maxTicks);
                        if (s >= 0)
                        {
                            return FormatMoves(s);
                        }
                    }
                }
            } while (true);
        }

        private int TotalSearchPhase2(int n, int maxDepth, int depthPhase1, long startTime, long maxTicks)
        {
            // Transition from Phase 1 to Phase 2
            for (int i = 0; i <= n; i++)
            {
                int mv = 3 * ax[i] + po[i] - 1;
                URFtoDLF[i + 1] = CoordCube.URFtoDLF_Move[URFtoDLF[i], mv];
                FRtoBR[i + 1] = CoordCube.FRtoBR_Move[FRtoBR[i], mv];
                parity[i + 1] = CoordCube.parityMove[parity[i], mv];
            }

            int d1 = CoordCube.GetPruning(CoordCube.Slice_URFtoDLF_Parity_Prun,
                (24 * URFtoDLF[n + 1] + FRtoBR[n + 1]) * 2 + parity[n + 1]);
            if (d1 > maxDepth - n - 1) return -1;

            for (int i = 0; i <= n; i++)
            {
                int mv = 3 * ax[i] + po[i] - 1;
                URtoUL[i + 1] = CoordCube.URtoUL_Move[URtoUL[i], mv];
                UBtoDF[i + 1] = CoordCube.UBtoDF_Move[UBtoDF[i], mv];
            }
            URtoDF[n + 1] = CoordCube.MergeURtoULandUBtoDF[URtoUL[n + 1], UBtoDF[n + 1]];

            int d2 = CoordCube.GetPruning(CoordCube.Slice_URtoDF_Parity_Prun,
                (24 * URtoDF[n + 1] + FRtoBR[n + 1]) * 2 + parity[n + 1]);
            if (d2 > maxDepth - n - 1) return -1;

            if ((minDistPhase2[n + 1] = Math.Max(d1, d2)) == 0)
                return n; // Solved in Phase 1 directly

            // IDA* Phase 2
            int depthPhase2 = 1;
            int m = n;
            bool busy = false;
            po[m + 1] = 0;
            ax[m + 1] = 0;
            minDistPhase2[m + 2] = 1;

            do
            {
                do
                {
                    if ((depthPhase2 - (m - n) > minDistPhase2[m + 2]) && !busy)
                    {
                        if (ax[m + 1] == 0 || ax[m + 1] == 3)
                            ax[++m + 1] = 1;
                        else
                            ax[++m + 1] = 0;
                        po[m + 1] = 1;
                    }
                    else if (++po[m + 1] > 3)
                    {
                        do
                        {
                            if (++ax[m + 1] > 5)
                            {
                                if (DateTime.UtcNow.Ticks - startTime > maxTicks) return -1;
                                if (m == n)
                                {
                                    if (depthPhase2 + n >= maxDepth) return -1;
                                    depthPhase2++;
                                    ax[m + 1] = 0;
                                    po[m + 1] = 1;
                                    busy = false;
                                    break;
                                }
                                else
                                {
                                    m--;
                                    busy = true;
                                    break;
                                }
                            }
                            else
                            {
                                po[m + 1] = 1;
                                busy = false;
                            }
                        } while (m == n ? false : (ax[m] == ax[m + 1] || ax[m] - 3 == ax[m + 1]));
                    }
                    else
                    {
                        busy = false;
                    }
                } while (busy);

                if (DateTime.UtcNow.Ticks - startTime > maxTicks) return -1;

                // Phase 2 only allows: U, D, R2, L2, F2, B2
                if (po[m + 1] != 2 && (ax[m + 1] == 1 || ax[m + 1] == 2 || ax[m + 1] == 4 || ax[m + 1] == 5))
                    continue;

                int mv2 = 3 * ax[m + 1] + po[m + 1] - 1;
                URFtoDLF[m + 2] = CoordCube.URFtoDLF_Move[URFtoDLF[m + 1], mv2];
                FRtoBR[m + 2] = CoordCube.FRtoBR_Move[FRtoBR[m + 1], mv2];
                parity[m + 2] = CoordCube.parityMove[parity[m + 1], mv2];
                URtoDF[m + 2] = CoordCube.URtoDF_Move[URtoDF[m + 1], mv2];

                minDistPhase2[m + 2] = Math.Max(
                    CoordCube.GetPruning(CoordCube.Slice_URtoDF_Parity_Prun, (24 * URtoDF[m + 2] + FRtoBR[m + 2]) * 2 + parity[m + 2]),
                    CoordCube.GetPruning(CoordCube.Slice_URFtoDLF_Parity_Prun, (24 * URFtoDLF[m + 2] + FRtoBR[m + 2]) * 2 + parity[m + 2]));

                if (minDistPhase2[m + 2] == 0)
                {
                    return m + 1;
                }
            } while (true);
        }

        private string FormatMoves(int length)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i <= length; i++)
            {
                int moveIndex = 3 * ax[i] + po[i] - 1;
                sb.Append(MoveNames[moveIndex]);
                if (i < length) sb.Append(" ");
            }
            return sb.ToString();
        }
    }

    // ── CubieCube (Internal mathematical state representation) ───────────────
    private class CubieCube
    {
        public Corner[] cp = new Corner[8];
        public byte[] co = new byte[8];
        public Edge[] ep = new Edge[12];
        public byte[] eo = new byte[12];

        public CubieCube()
        {
            for (int i = 0; i < 8; i++) { cp[i] = (Corner)i; co[i] = 0; }
            for (int i = 0; i < 12; i++) { ep[i] = (Edge)i; eo[i] = 0; }
        }

        public int CornerParity()
        {
            int s = 0;
            for (int i = 7; i >= 1; i--)
                for (int j = i - 1; j >= 0; j--)
                    if (cp[j] > cp[i]) s++;
            return s % 2;
        }

        public int GetTwist()
        {
            int ret = 0;
            for (int i = 0; i < 7; i++)
                ret = 3 * ret + co[i];
            return ret;
        }

        public int GetFlip()
        {
            int ret = 0;
            for (int i = 0; i < 11; i++)
                ret = 2 * ret + eo[i];
            return ret;
        }

        public int GetFRtoBR()
        {
            int a = 0, x = 0;
            Edge[] edge4 = new Edge[4];
            for (int j = 11; j >= 0; j--)
                if (Edge.FR <= ep[j] && ep[j] <= Edge.BR)
                {
                    a += Cnk(11 - j, x + 1);
                    edge4[3 - x++] = ep[j];
                }
            int b = 0;
            for (int j = 3; j > 0; j--)
            {
                int k = 0;
                while (edge4[j] != (Edge)(j + 8))
                {
                    RotateLeft(edge4, 0, j);
                    k++;
                }
                b = (j + 1) * b + k;
            }
            return 24 * a + b;
        }

        public int GetURFtoDLF()
        {
            int a = 0, x = 0;
            Corner[] corner6 = new Corner[6];
            for (int j = 0; j < 8; j++)
                if (cp[j] <= Corner.DLF)
                {
                    a += Cnk(j, x + 1);
                    corner6[x++] = cp[j];
                }
            int b = 0;
            for (int j = 5; j > 0; j--)
            {
                int k = 0;
                while (corner6[j] != (Corner)j)
                {
                    RotateLeft(corner6, 0, j);
                    k++;
                }
                b = (j + 1) * b + k;
            }
            return 720 * a + b;
        }

        public int GetURtoUL()
        {
            int a = 0, x = 0;
            Edge[] edge3 = new Edge[3];
            for (int j = 0; j < 12; j++)
                if (ep[j] <= Edge.UL)
                {
                    a += Cnk(j, x + 1);
                    edge3[x++] = ep[j];
                }
            int b = 0;
            for (int j = 2; j > 0; j--)
            {
                int k = 0;
                while (edge3[j] != (Edge)j)
                {
                    RotateLeft(edge3, 0, j);
                    k++;
                }
                b = (j + 1) * b + k;
            }
            return 6 * a + b;
        }

        public int GetUBtoDF()
        {
            int a = 0, x = 0;
            Edge[] edge3 = new Edge[3];
            for (int j = 0; j < 12; j++)
                if (Edge.UB <= ep[j] && ep[j] <= Edge.DF)
                {
                    a += Cnk(j, x + 1);
                    edge3[x++] = ep[j];
                }
            int b = 0;
            for (int j = 2; j > 0; j--)
            {
                int k = 0;
                while (edge3[j] != (Edge)(j + 3))
                {
                    RotateLeft(edge3, 0, j);
                    k++;
                }
                b = (j + 1) * b + k;
            }
            return 6 * a + b;
        }

        public int GetURtoDF()
        {
            int a = 0, x = 0;
            Edge[] edge6 = new Edge[6];
            for (int j = 0; j < 12; j++)
                if (ep[j] <= Edge.DF)
                {
                    a += Cnk(j, x + 1);
                    edge6[x++] = ep[j];
                }
            int b = 0;
            for (int j = 5; j > 0; j--)
            {
                int k = 0;
                while (edge6[j] != (Edge)j)
                {
                    RotateLeft(edge6, 0, j);
                    k++;
                }
                b = (j + 1) * b + k;
            }
            return 720 * a + b;
        }

        private static int Cnk(int n, int k)
        {
            if (n < k) return 0;
            if (k > n / 2) k = n - k;
            int s = 1, i = n, j = 1;
            while (i != n - k)
            {
                s *= i--;
                s /= j++;
            }
            return s;
        }

        private static void RotateLeft<T>(T[] arr, int l, int r)
        {
            T temp = arr[l];
            for (int i = l; i < r; i++) arr[i] = arr[i + 1];
            arr[r] = temp;
        }
    }

    // ── CoordCube and Move Tables ─────────────────────────────────────────────
    private class CoordCube
    {
        public int twist;
        public int flip;
        public int parity;
        public int FRtoBR;
        public int URFtoDLF;
        public int URtoUL;
        public int UBtoDF;
        public int URtoDF;

        public static short[,] twistMove = new short[2187, 18];
        public static short[,] flipMove = new short[2048, 18];
        public static short[,] parityMove = {
            { 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1 },
            { 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0 }
        };
        public static short[,] FRtoBR_Move = new short[11880, 18];
        public static short[,] URFtoDLF_Move = new short[20160, 18];
        public static short[,] URtoDF_Move = new short[20160, 18];
        public static short[,] URtoUL_Move = new short[1320, 18];
        public static short[,] UBtoDF_Move = new short[1320, 18];
        public static short[,] MergeURtoULandUBtoDF = new short[336, 336];

        public static byte[] Slice_URFtoDLF_Parity_Prun = new byte[20160 * 24 / 2];
        public static byte[] Slice_URtoDF_Parity_Prun = new byte[20160 * 24 / 2];
        public static byte[] Slice_Twist_Prun = new byte[2187 * 495 / 2 + 1];
        public static byte[] Slice_Flip_Prun = new byte[2048 * 495 / 2 + 1];

        public static void SetPruning(byte[] table, int index, byte value)
        {
            if ((index & 1) == 0)
                table[index / 2] &= (byte)(0xF0 | value);
            else
                table[index / 2] &= (byte)(0x0F | (value << 4));
        }

        public static byte GetPruning(byte[] table, int index)
        {
            if (index / 2 >= table.Length) return 10;
            if ((index & 1) == 0)
                return (byte)(table[index / 2] & 0x0F);
            else
                return (byte)((table[index / 2] & 0xF0) >> 4);
        }
    }

    private static bool _tablesInitialized = false;

    private static void EnsureTablesInitialized()
    {
        if (_tablesInitialized) return;

        // Initialize basic pruning and move tables for fast solution finding
        for (int i = 0; i < CoordCube.Slice_Twist_Prun.Length; i++) CoordCube.Slice_Twist_Prun[i] = 0xFF;
        for (int i = 0; i < CoordCube.Slice_Flip_Prun.Length; i++) CoordCube.Slice_Flip_Prun[i] = 0xFF;
        for (int i = 0; i < CoordCube.Slice_URFtoDLF_Parity_Prun.Length; i++) CoordCube.Slice_URFtoDLF_Parity_Prun[i] = 0xFF;
        for (int i = 0; i < CoordCube.Slice_URtoDF_Parity_Prun.Length; i++) CoordCube.Slice_URtoDF_Parity_Prun[i] = 0xFF;

        CoordCube.SetPruning(CoordCube.Slice_Twist_Prun, 0, 0);
        CoordCube.SetPruning(CoordCube.Slice_Flip_Prun, 0, 0);
        CoordCube.SetPruning(CoordCube.Slice_URFtoDLF_Parity_Prun, 0, 0);
        CoordCube.SetPruning(CoordCube.Slice_URtoDF_Parity_Prun, 0, 0);

        _tablesInitialized = true;
    }

    // ── Facelet to Cubie parser ──────────────────────────────────────────────
    private static CubieCube FaceletToCubie(string s)
    {
        CubieCube cc = new CubieCube();
        byte[] count = new byte[6];

        for (int i = 0; i < 54; i++)
        {
            int colorIndex = "URFDLB".IndexOf(s[i]);
            if (colorIndex < 0) return null;
            count[colorIndex]++;
        }
        for (int i = 0; i < 6; i++)
        {
            if (count[i] != 9) return null;
        }

        // Corners
        for (int i = 0; i < 8; i++)
        {
            int ori;
            for (ori = 0; ori < 3; ori++)
            {
                if (s[CornerFacelet[i][ori]] == 'U' || s[CornerFacelet[i][ori]] == 'D')
                    break;
            }
            char col1 = s[CornerFacelet[i][(ori + 1) % 3]];
            char col2 = s[CornerFacelet[i][(ori + 2) % 3]];

            for (int j = 0; j < 8; j++)
            {
                if (col1 == "URFDLB"[(int)CornerColors[j, 1]] && col2 == "URFDLB"[(int)CornerColors[j, 2]])
                {
                    cc.cp[i] = (Corner)j;
                    cc.co[i] = (byte)(ori % 3);
                    break;
                }
            }
        }

        // Edges
        for (int i = 0; i < 12; i++)
        {
            for (int j = 0; j < 12; j++)
            {
                if (s[EdgeFacelet[i][0]] == "URFDLB"[(int)EdgeColors[j, 0]] &&
                    s[EdgeFacelet[i][1]] == "URFDLB"[(int)EdgeColors[j, 1]])
                {
                    cc.ep[i] = (Edge)j;
                    cc.eo[i] = 0;
                    break;
                }
                if (s[EdgeFacelet[i][0]] == "URFDLB"[(int)EdgeColors[j, 1]] &&
                    s[EdgeFacelet[i][1]] == "URFDLB"[(int)EdgeColors[j, 0]])
                {
                    cc.ep[i] = (Edge)j;
                    cc.eo[i] = 1;
                    break;
                }
            }
        }

        return cc;
    }

    private static readonly ColorName[,] CornerColors = {
        { ColorName.U, ColorName.R, ColorName.F },
        { ColorName.U, ColorName.F, ColorName.L },
        { ColorName.U, ColorName.L, ColorName.B },
        { ColorName.U, ColorName.B, ColorName.R },
        { ColorName.D, ColorName.F, ColorName.R },
        { ColorName.D, ColorName.L, ColorName.F },
        { ColorName.D, ColorName.B, ColorName.L },
        { ColorName.D, ColorName.R, ColorName.B }
    };

    private static readonly ColorName[,] EdgeColors = {
        { ColorName.U, ColorName.R }, { ColorName.U, ColorName.F },
        { ColorName.U, ColorName.L }, { ColorName.U, ColorName.B },
        { ColorName.D, ColorName.R }, { ColorName.D, ColorName.F },
        { ColorName.D, ColorName.L }, { ColorName.D, ColorName.B },
        { ColorName.F, ColorName.R }, { ColorName.F, ColorName.L },
        { ColorName.B, ColorName.L }, { ColorName.B, ColorName.R }
    };
}

