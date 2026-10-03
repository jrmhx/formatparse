using System.Text;

using FormatParse;

namespace FormatParse.Tests;

public sealed class WideParsingTests
{
    [Fact]
    public void MoreThan128FieldsUsePooledCaptureStorage()
    {
        StringBuilder pattern = new();
        StringBuilder input = new();
        for (int index = 0; index < 129; index++)
        {
            if (index > 0)
            {
                string separator = $"<#separator{index}#>";
                pattern.Append(separator);
                input.Append(separator);
            }

            pattern.Append("{}");
            input.Append('1');
        }

        string format = pattern.ToString();
        string text = input.ToString();
        FormatParser<WideTarget> parser = Parser.Compile<WideTarget>(format);
        Assert.Equal(129, parser.Parse(text).Sum);
        Assert.False(parser.TryParse(text + "invalid", out _));
        Assert.False(parser.TryParse("wrong prefix", out _));
        Assert.Equal(129, parser.Parse(text).Sum);

        Parallel.For(0, 100, _ => Assert.Equal(129, parser.Parse(text).Sum));
    }

    private sealed class WideTarget
    {
        public WideTarget(
            int P0, int P1, int P2, int P3,
            int P4, int P5, int P6, int P7,
            int P8, int P9, int P10, int P11,
            int P12, int P13, int P14, int P15,
            int P16, int P17, int P18, int P19,
            int P20, int P21, int P22, int P23,
            int P24, int P25, int P26, int P27,
            int P28, int P29, int P30, int P31,
            int P32, int P33, int P34, int P35,
            int P36, int P37, int P38, int P39,
            int P40, int P41, int P42, int P43,
            int P44, int P45, int P46, int P47,
            int P48, int P49, int P50, int P51,
            int P52, int P53, int P54, int P55,
            int P56, int P57, int P58, int P59,
            int P60, int P61, int P62, int P63,
            int P64, int P65, int P66, int P67,
            int P68, int P69, int P70, int P71,
            int P72, int P73, int P74, int P75,
            int P76, int P77, int P78, int P79,
            int P80, int P81, int P82, int P83,
            int P84, int P85, int P86, int P87,
            int P88, int P89, int P90, int P91,
            int P92, int P93, int P94, int P95,
            int P96, int P97, int P98, int P99,
            int P100, int P101, int P102, int P103,
            int P104, int P105, int P106, int P107,
            int P108, int P109, int P110, int P111,
            int P112, int P113, int P114, int P115,
            int P116, int P117, int P118, int P119,
            int P120, int P121, int P122, int P123,
            int P124, int P125, int P126, int P127,
            int P128)
        {
            Sum =
                P0 + P1 + P2 + P3 + P4 + P5 + P6 + P7 +
                P8 + P9 + P10 + P11 + P12 + P13 + P14 + P15 +
                P16 + P17 + P18 + P19 + P20 + P21 + P22 + P23 +
                P24 + P25 + P26 + P27 + P28 + P29 + P30 + P31 +
                P32 + P33 + P34 + P35 + P36 + P37 + P38 + P39 +
                P40 + P41 + P42 + P43 + P44 + P45 + P46 + P47 +
                P48 + P49 + P50 + P51 + P52 + P53 + P54 + P55 +
                P56 + P57 + P58 + P59 + P60 + P61 + P62 + P63 +
                P64 + P65 + P66 + P67 + P68 + P69 + P70 + P71 +
                P72 + P73 + P74 + P75 + P76 + P77 + P78 + P79 +
                P80 + P81 + P82 + P83 + P84 + P85 + P86 + P87 +
                P88 + P89 + P90 + P91 + P92 + P93 + P94 + P95 +
                P96 + P97 + P98 + P99 + P100 + P101 + P102 + P103 +
                P104 + P105 + P106 + P107 + P108 + P109 + P110 + P111 +
                P112 + P113 + P114 + P115 + P116 + P117 + P118 + P119 +
                P120 + P121 + P122 + P123 + P124 + P125 + P126 + P127 +
                P128;
        }

        public int Sum { get; }
    }
}
