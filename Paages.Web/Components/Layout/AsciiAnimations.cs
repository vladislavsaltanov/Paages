namespace Paages.Web.Components.Layout;

public static class AsciiAnimations
{
    public const string Arrows = "arrows";

    public static readonly IReadOnlyDictionary<string, string[]> Frames = new Dictionary<string, string[]>
    {
        [Arrows] =
        [
            "[>    ]",
            "[>>   ]",
            "[>>>  ]",
            "[ >>> ]",
            "[  >>>]",
            "[   >>]",
            "[    >]",
            "[     ]"
        ]
    };
}