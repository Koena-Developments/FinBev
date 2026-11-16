namespace PettyCash.Application.Common;

public static class StaffEmojiCatalog
{
    public static IReadOnlyList<EmojiGroup> Groups { get; } =
    [
        new("smileys", "Smileys", "😀",
        [
            "😀", "😃", "😄", "😁", "😆", "😅", "🤣", "😂", "🙂", "😉", "😊", "😇", "🥰", "😍", "🤩", "😘",
            "😋", "😜", "🤪", "🤗", "🤔", "😐", "😏", "😌", "😴", "😎", "🤓", "🥳", "😮", "🥺", "😢", "😤"
        ]),
        new("people", "People", "👋",
        [
            "👋", "👋🏻", "👋🏼", "👋🏽", "👋🏾", "👋🏿", "👍", "👍🏻", "👍🏼", "👍🏽", "👍🏾", "👍🏿", "👏", "🙌", "🤝", "🙏",
            "💪", "👀", "🧠", "🧑", "👩", "👨", "👧", "👦", "🧑‍💻", "👩‍💼", "👨‍💼", "🧑‍🏫", "👩‍🔬", "🧑‍🍳", "👷", "🧑‍🌾"
        ]),
        new("nature", "Nature", "🐶",
        [
            "🐶", "🐱", "🐭", "🐰", "🦊", "🐻", "🐼", "🐨", "🦁", "🐯", "🐮", "🐷", "🐸", "🐵", "🐧", "🐦",
            "🦅", "🦉", "🐝", "🦋", "🌸", "🌻", "🌷", "🌹", "🍀", "🌳", "🌴", "🌵", "🌙", "⭐", "☀️", "🌈"
        ]),
        new("food", "Food", "🍎",
        [
            "🍎", "🍐", "🍊", "🍋", "🍌", "🍉", "🍇", "🍓", "🍒", "🍑", "🥭", "🍍", "🥑", "🥕", "🌽", "🍞",
            "🥐", "🧀", "🍔", "🍕", "🥗", "🍝", "🍣", "🍪", "🍰", "🧁", "☕", "🍵", "🧃", "🥤"
        ]),
        new("activity", "Activity", "⚽",
        [
            "⚽", "🏀", "🏈", "⚾", "🎾", "🏐", "🏉", "🎱", "🏓", "🏸", "🥅", "⛳", "🏆", "🥇", "🎯", "🎮",
            "🎲", "🧩", "🎨", "🎤", "🎧", "🎸", "🎹", "📚", "✏️", "💼"
        ]),
        new("travel", "Travel", "🚗",
        [
            "🚗", "🚕", "🚌", "🚲", "✈️", "🚀", "⛵", "🏠", "🏢", "🏫", "🏥", "🏦", "🏪", "🌍", "🌎", "🌏",
            "🗺️", "⛰️", "🏖️", "🌅", "🌇", "🌃"
        ]),
        new("objects", "Objects", "💡",
        [
            "💡", "📌", "📎", "📁", "📅", "📈", "📉", "🧾", "💳", "💰", "🪙", "📦", "🔒", "🔑", "🛠️", "⚙️",
            "🔬", "💻", "📱", "⌚", "📷", "🎁", "🔔", "✉️"
        ]),
        new("symbols", "Symbols", "❤️",
        [
            "❤️", "🧡", "💛", "💚", "💙", "💜", "🖤", "🤍", "🤎", "💖", "✨", "🔥", "💯", "✅", "⭐", "🌟",
            "⚡", "☀️", "❄️", "💧", "🎵", "🔔", "🏁", "🇿🇦"
        ])
    ];

    private static readonly HashSet<string> Allowed = Groups
        .SelectMany(group => group.Emojis)
        .ToHashSet(StringComparer.Ordinal);

    public static bool TryNormalize(string? value, out string emoji)
    {
        emoji = value?.Trim() ?? "";
        return emoji.Length is > 0 and <= 32 && Allowed.Contains(emoji);
    }

    public sealed record EmojiGroup(string Id, string Label, string Icon, IReadOnlyList<string> Emojis);
}
