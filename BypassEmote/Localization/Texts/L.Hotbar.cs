using NoireLib.Localizer;

namespace BypassEmote.Localization;

internal static partial class L
{
    public static readonly NoireString Assign = new("hotbar.assign", "Assign");
    public static readonly NoireString HotbarLabel = new("hotbar.hotbar", "Hotbar");
    public static readonly NoireString SlotEmpty = new("hotbar.slot.empty", "Slot {slot} - Empty");
    public static readonly NoireString SlotHolds = new("hotbar.slot.holds", "Slot {slot} - {name}");

    public static readonly NoireString ClassicDragHint = new("classic.hotbar.drag_hint",
        "You can also drag and drop an emote from the main window onto any visible hotbar slot.");
    public static readonly NoireString ClassicAssignTitle = new("classic.hotbar.title", "Assign {emote} to hotbar...");
    public static readonly NoireString CrossHotbarShort = new("classic.hotbar.cross", "XHB {number}");
    public static readonly NoireString CurrentlyAssigned = new("classic.hotbar.assigned", "Currently assigned: {name}");
    public static readonly NoireString ClassicSlotEmpty = new("classic.hotbar.slot_empty", "This slot is empty. You can safely assign an emote.");

    public static readonly NoireString SilkDragHint = new("silk.hotbar.drag_hint", "You can also drag an emote from the main window onto any visible hotbar slot.");
    public static readonly NoireString Standard = new("silk.hotbar.standard", "Standard");
    public static readonly NoireString Cross = new("silk.hotbar.cross", "Cross");
    public static readonly NoireString LeftTrigger = new("silk.hotbar.left_trigger", "LEFT TRIGGER");
    public static readonly NoireString RightTrigger = new("silk.hotbar.right_trigger", "RIGHT TRIGGER");
    public static readonly NoireString PickASlot = new("silk.hotbar.pick_slot", "Pick a slot.");
    public static readonly NoireString PickASlotFor = new("silk.hotbar.pick_slot_for", "Pick a slot for {emote}.");
    public static readonly NoireString ThisEmote = new("silk.hotbar.this_emote", "this emote");
    public static readonly NoireString HotbarSlotEmpty = new("silk.hotbar.state.empty", "Hotbar {bar}, slot {slot} is empty. You can safely assign the emote.");
    public static readonly NoireString HotbarSlotHolds = new("silk.hotbar.state.holds", "Hotbar {bar}, slot {slot} holds {name}. Assigning replaces it.");
    public static readonly NoireString CrossSlotEmpty = new("silk.hotbar.state.cross_empty", "Cross hotbar {bar}, slot {slot} is empty. You can safely assign the emote.");
    public static readonly NoireString CrossSlotHolds = new("silk.hotbar.state.cross_holds", "Cross hotbar {bar}, slot {slot} holds {name}. Assigning replaces it.");

    public static string Fill(NoireString text, string name1, string value1, string name2, string value2, string name3, string value3)
        => text.With(name1, value1, name2, value2).Replace("{" + name3 + "}", value3);
}
