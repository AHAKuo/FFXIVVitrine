# TODO

## V1 implemented (2026-10-15, inside Lamina before the split)
- [x] Mod catalog with classification, incremental scan, cache.
- [x] Auto designs per mod (deterministic ids), baselines, variants, `designs.json`.
- [x] `/vit` window: Mods (card grid + details), Collections, Settings, About.
- [x] Own plugin project, config directory and repo (`AHAKuo/FFXIVVitrine`); IPC for Lamina (`docs/IPC.md`); preview writes with Vitrine's own Penumbra key; released v0.1.0 and registered in ahadevtools.

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

## Try on replaced hair (and other customization) instantly
Hair mods list affected items like "Customization: Midlander Female Hair 194". Preview enables the mod but the
character only shows it when wearing that hairstyle.
- [ ] Parse customization affected items (race, clan, gender, kind: Hair / Face / Tail / Ears, number) from the name.
- [ ] If the entry matches the player's clan and gender, make the chip clickable and have Preview apply it: Glamourer
      `ApplyState` with a FileVersion 1 state that sets only that customization value (`Hairstyle` = 194, `Apply` = true)
      with flags Customization | Once (not persisted, no lock; retry with Lamina's key on InvalidKey like SetItem).
- [ ] Entries for another clan / gender are shown but disabled with a tooltip ("made for Miqo'te female"); when a mod
      ships several variants (194 and 195), the first matching one is used for Preview and each is clickable.
- [ ] "Revert try-on" and "End preview" revert it like gear (`RevertToAutomation`); Lamina hand-back unchanged.
- [ ] **Eyes**: entries like "Customization: Au Ra Female Face (Iris) 1" are per-face iris textures. Parse the "(Iris)" /
      "(Eyes)" marker and the face number; the try-on applies `Face` = N for the matching clan / gender (if the
      character already uses that face nothing changes and the mod simply shows). Many eye packs need a base eye colour
      ("set eyes to white when using options 1-10"): add a quick eye-colour picker in the TRY ON row (right / left for
      odd eyes, applied with Customization | Once) so the pack can be judged without opening Glamourer.
- [ ] Same parsing covers "(Etc)", "(Tail)", "(Ears)" markers where mods use them; unknown markers fall back to the
      plain Face / Hair handling.

## Verify in game
- [ ] Mod manager adapter returns metadata (else meta.json fallback); changed-item value types classify correctly; images load.
- [ ] Photo fetching against real mod pages (XIV Mod Archive, Heliosphere); JPG/PNG decoding.
- [ ] `ExecuteEmote` argument semantics for emote previews.
- [ ] Penumbra accepting an empty settings dictionary as "defaults" for previews.
