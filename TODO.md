# TODO

## V1 implemented (2026-10-15, inside Lamina before the split)
- [x] Mod catalog with classification, incremental scan, cache.
- [x] Auto designs per mod (deterministic ids), baselines, variants, `designs.json`.
- [x] `/vit` window: Mods (card grid + details), Collections, Settings, About.
- [x] Own plugin project, config directory and repo (`AHAKuo/FFXIVVitrine`); IPC for Lamina (`docs/IPC.md`); preview writes with Vitrine's own Penumbra key; released v0.1.0 and registered in ahadevtools.

## Apply Action (Vitrine special) — implemented 2026-10-15
An **Apply Action** is configured per design, in the baseline settings and in each variant. It is different from
Try-on: Try-on guesses one item per slot to show the mod; the Apply Action is the user's explicit choice of which
pieces a design should equip when it is applied.

- [x] **Slot control**: per-slot toggles in the baseline / variant editor ("Apply action" block); default = every slot the
      mod affects with its first piece (`ApplyPlanner.Default`), "Customise" switches to explicit slots, "Use default" goes back.
- [x] **Piece selection**: a combo per enabled slot listing the mod's pieces for that slot plus "none"; a second ring offers
      the left finger. Stored on `Baseline.ApplyAction` / `StoredVariant.ApplyAction`, exported as `VitrineDesign.ApplyAction`.
- [x] **Apply button** next to Preview (and on variant cards): enables the mod with the design's settings, redraws and equips
      exactly the planned pieces (`ApplyPlanner.Plan`; pieces the mod no longer offers are reported, not guessed); fitting
      hair / face entries are applied too; no emotes.
- [x] **IPC**: `applyAction` on the design JSON, new `Vitrine.ApplyDesign` gate, version `(1, 1)`, `docs/IPC.md` updated.
- [x] Try-on stays as is for quick looks; "Apply" is the deliberate one.

## "Make Glamourer design" button — implemented 2026-10-15
- [x] `GlamourerDesignBuilder` builds the FileVersion 3 JSON: pieces (Apply Action, else full-set plan) as equipment with
      `Apply = true`, fitting hair / face / tail values as customize with `Apply = true`, nothing else applied, and the mod
      association (`Mods`: name, directory, `State`, priority, every option group with each option Enabled / Disabled; no
      `Settings` when the design uses Penumbra defaults).
- [x] Created through `AddDesign(json, "<Mod> · Baseline" / "<Mod> · <Variant>")`; the status line reports it and an
      "Open in Glamourer" button (`OpenDesign`) appears under the design buttons.
- [x] Button on the baseline ("Make Glamourer design") and on every variant card ("Glamourer design"); disabled with a
      tooltip when Glamourer is missing or nothing is equippable (no pieces and no fitting customization).

## Try on replaced hair (and other customization) instantly — implemented 2026-10-15
- [x] `CustomizationParser` reads "Customization: <Race> <Gender> <Part> [(Marker)] <N>" (model races as Penumbra names them:
      Midlander, Highlander, Elezen, Lalafell, Miqo'te, Roegadyn, Au Ra, Hrothgar, Viera; clan names and the old
      "<Race> <Clan> <Gender>" form are accepted too). Kinds: Hair → `Hairstyle`, Face → `Face`, Tail / Ears → `TailShape`.
- [x] Entries fitting the player's race (from the customize bytes: clan byte 4, gender byte 1) are clickable chips; Preview
      and Apply apply the first fitting entry per kind through `ApplyState` (FileVersion 1 state, only that value applied,
      identity copied unapplied so Glamourer validates against the right clan; flags Customization | Once; Lamina-key retry on
      InvalidKey).
- [x] Entries for another race / gender are greyed out with "Made for Au Ra female; your character is …"; several variants
      (194 and 195) are each clickable, Preview uses the first.
- [x] "Revert try-on" and "End preview" revert via `RevertToAutomation` (customization included); Lamina hand-back unchanged.
- [x] **Eyes**: "(Iris)" / "(Eyes)" markers are parsed; the chip sets `Face` = N. "Eye colour…" in the TRY ON row opens a
      192-swatch picker (colours from `human.cmp`, current values outlined) with Both / Right / Left for odd eyes, applied with
      Customization | Once.
- [x] "(Tail)" / "(Ears)" markers switch the kind; other markers ("(Etc)") keep the plain Face / Hair handling. Body entries
      parse but have no Glamourer value (texture only) and stay in the summary chip.

## Card rework (2026-10-15)
- [x] Bottom button block removed; every card (Baseline, variants) carries Edit · Copy id (· Delete) and Preview / End preview ·
      Enable / Disable · Glamourer design. "Edit baseline" is "Edit". Glamourer designs are created directly (no "Open in Glamourer").
- [x] Preview is transient: one at a time, toggles to "End preview", ended on window close (`OnClose`) and on unload. TRY ON row
      keeps only the eye colour picker and the replaced emote.
- [x] Enable / Disable write the card's settings permanently into the player's collection (TrySetMod / Priority / Settings);
      `designs.json` remembers the active design per mod and the card is highlighted green while the mod is on.
- [x] "Affected slots" in the editor: slot toggle chips, piece pickers for slots with alternatives, "All" to reset. IPC 1.2 exports
      the resolved `pieces` so Lamina can layer them with its own slot mask.

## Clear other slots (2026-10-15)
- [x] Per baseline / variant flag; Preview and the exported Glamourer design apply Glamourer's "Nothing" to every gear slot the
      pieces do not fill (weapons excluded). Exported over IPC 1.3 as `clearOtherSlots`; Lamina honours and can override it.

## Verify in game
- [ ] Mod manager adapter returns metadata (else meta.json fallback); changed-item value types classify correctly; images load.
- [ ] Photo fetching against real mod pages (XIV Mod Archive, Heliosphere); JPG/PNG decoding.
- [ ] `ExecuteEmote` argument semantics for emote previews.
- [ ] Penumbra accepting an empty settings dictionary as "defaults" for previews.
- [ ] Affected slots editor: slot chips and piece pickers listed per mod; Preview equips only enabled slots; variants inherit
      the baseline's slots on creation. Enable writes priority and every option group (Penumbra return codes in the status line).
- [ ] `AddDesign` accepts the generated JSON (mod association without option `Identifier`s: Glamourer should match groups
      and options by name; if it rejects or drops them, read the ids from Penumbra and add them). Design shows in Glamourer's
      list and "Open in Glamourer" selects it.
- [ ] Customization names as Penumbra really emits them for hair, face, tail, ears, iris; face numbers for second clans
      (Xaela f0101…: Penumbra may name them "Face 101" while Glamourer's `Face` value is 5..8) — add a mapping if so.
- [ ] `ApplyState` with the FileVersion 1 customization-only state: hair / face change immediately, gear untouched, revert
      restores; works while Lamina holds the lock (key retry).
- [ ] "Clear other slots": Glamourer accepts the Nothing ids through SetItem and in design files; rings and ears empty as expected.
- [ ] Eye colour swatches match the game's palette (offset in `human.cmp`); right / left applied as chosen.
