namespace BypassEmote.EmoteSwap;

// Address arithmetic for the character's bound pack sets
internal static class PackScanLayout
{
    internal const int VectorsOffset = 8;

    internal const int Groups = 2;
    internal const int SlotsPerGroup = 5;
    internal const int GroupStride = 0x78;
    internal const int SlotStride = 0x18;

    internal const int PackNameCountOffset = 0xD8;
    internal const int PackNameTableOffset = 0xF0;
    internal const int PackNameEntryStride = 0x28;

    // Where an entry stores its own index into the holder's tables. Not its position in the name table.
    internal const int PackNameEntryTableIndexOffset = 0x22;

    internal const int PackAnimationTableOffset = 0x110;
    internal const int PackHavokHolderOffset = 0xC0;
    internal const int PackHavokTableOffset = 0x30;

    // Bone mappings live beside the havok animations, in the same holder, one table earlier.
    internal const int PackMappingTableOffset = 0x20;

    internal const int MaxPacksPerSlot = 64;

    internal const int MaxPackNames = 512;

    internal static long VectorAddress(long packSet, int group, int slot)
        => packSet + VectorsOffset + (long)group * GroupStride + (long)slot * SlotStride;

    // We cannot make the vcall that returns the slot count, so check the run looks sane instead.
    internal static bool IsPlausibleVector(long begin, long end)
    {
        if (begin == 0 && end == 0)
            return true;

        if (begin == 0 || end < begin)
            return false;

        var span = end - begin;
        return span % 8 == 0 && span / 8 <= MaxPacksPerSlot;
    }

    internal static int PackCount(long begin, long end)
        => begin == 0 || end < begin ? 0 : (int)((end - begin) / 8);

    internal static bool IsPlausiblePackNameTable(int count, long table)
        => count > 0 && count <= MaxPackNames && table != 0;

    internal static long PackNameEntry(long table, int index)
        => table + (long)index * PackNameEntryStride;

    internal static long PackAnimationSlot(long animationTable, int index)
        => animationTable + (long)index * 8;

    internal static long PackHavokTable(long holder) => holder + PackHavokTableOffset;

    internal static long PackMappingTable(long holder) => holder + PackMappingTableOffset;

    internal static long PackNameEntryTableIndex(long table, int ordinal)
        => PackNameEntry(table, ordinal) + PackNameEntryTableIndexOffset;

    // The stored index, or -1 when the entry has no table slot.
    internal static int TableIndexOf(ushort stored) => (short)stored < 0 ? -1 : stored;
}
