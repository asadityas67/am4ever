// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.App.Wizard;

/// <summary>The step-6 flashing checklist shown to the beginner (design §6.6).</summary>
public static class FlashChecklist
{
    public const string Text = """
        Flashing checklist — read every line before you write the chip:

        1. Programmer voltage: a CH341A must be 3.3 V. The common CH341A boards power the
           chip at 5 V by default, which can damage it — use a 3.3 V adapter or a modified board.
        2. Back up first: read the chip and SAVE the original dump somewhere safe. If anything
           goes wrong, writing the original back is your recovery.
        3. Erase, write, then VERIFY with your programmer software. Do not skip verify.
        4. Clear CMOS after flashing (jumper or pull the battery for a minute).
        5. First boot can take 1–2 minutes with no display while memory trains — be patient.
        6. If it does not boot: re-flash the original backup, then report back with the build
           report saved next to your image.
        """;
}
