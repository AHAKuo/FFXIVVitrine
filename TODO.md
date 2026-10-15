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

## Verify in game
- [ ] Photo fetching against real mod pages (XIV Mod Archive, Heliosphere); JPG/PNG decoding.
- [ ] `ExecuteEmote` argument semantics for emote previews.
- [ ] Penumbra accepting an empty settings dictionary as "defaults" for previews.
