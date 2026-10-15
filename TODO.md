# TODO

## Apply Action (Vitrine special)
An **Apply Action** is configured per design, in the baseline settings and in each variant. It is different from
Try-on: Try-on guesses one item per slot to show the mod; the Apply Action is the user's explicit choice of which
pieces a design should equip when it is applied.

- [ ] **Slot control**: let the user choose which equipment slots a baseline or variant applies to (per-slot toggles,
      default = every slot the mod affects).
- [ ] **Piece selection**: for each enabled slot, let the user pick the exact affected item to equip (when a mod
      offers several bodies / legs / etc.), with "none" allowed. Stored with the design and exported over IPC.
- [ ] **Apply button** next to Preview: enable the mod with the design's settings and equip exactly the selected
      pieces through Glamourer (no guessing, no emotes unless selected).
- [ ] **IPC**: extend the design JSON with `applyAction` (`slots`: slot → item id / name, `enabled` flags) and bump the
      minor API version; keep `docs/IPC.md` in sync so Lamina can read it.
- [ ] Try-on stays as is for quick looks; "Apply" is the deliberate one.

## "Make Glamourer design" button
A small button next to "Copy design id" that builds a Glamourer design from this mod's output, so the user never has to
capture the look by hand.
- [ ] Build a Glamourer design JSON (FileVersion 3 layout, `Identifier` + `Name`): the design's selected pieces (Apply
      Action if set, else the full-set plan) as equipment with `Apply = true`, nothing else applied, plus the mod
      association (`Mods`: directory, name, enabled, priority, settings from the baseline / variant) so the design
      carries the mod with it.
- [ ] Create it through `Glamourer.Api` `AddDesign(designData, designName)` (name = "<Mod> · <Variant>"), report the new
      design id and offer "Open in Glamourer" (`OpenDesign`).
- [ ] Works for baselines and variants; disabled with a tooltip when Glamourer is missing or nothing is equippable.

## Verify in game
- [ ] Photo fetching against real mod pages (XIV Mod Archive, Heliosphere); JPG/PNG decoding.
- [ ] `ExecuteEmote` argument semantics for emote previews.
- [ ] Penumbra accepting an empty settings dictionary as "defaults" for previews.
