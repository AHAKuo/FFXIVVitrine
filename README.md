## About
Vitrine is a Dalamud plugin that shows your Penumbra mods the way a shop window would: every mod becomes a card with its display photo, organised by what it changes, and you can try any of them on your character in one click without opening Penumbra or Glamourer.

Dump mods into Penumbra like a mad man; Vitrine keeps them findable.

## Requires
- Penumbra.

## Supports
- Glamourer (equips a mod's gear, applies its hair / face, and receives designs made from a mod).
- Lamina (uses Vitrine designs as layers in its virtual designs, see below).

## Features
- **Mods tab:** a card grid of every mod, classified by category (outfits, accessories, hair, face, body, skin, animations, emotes, VFX, SFX, UI, minions, mounts, housing, NPCs), with search, category chips, sort and an on/off filter.
- **Display photos:** taken from the mod folder when it has one, otherwise fetched once from the mod's page (its link-preview image) and cached.
- **Details panel:** author, version, mod page, what the mod affects, its state in your collections, and the designs made from it.
- **Vitrine designs:** every mod gets an auto design with the baseline option settings you choose; variants keep the same mod with different options.
- **Preview on character:** enables the mod on you (a temporary Penumbra setting under Vitrine's own key), redraws, equips one affected item per equipment slot through Glamourer so a complete look shows, applies the hair / face entries that fit your character, and plays the replaced emote for emote mods. Click any affected item to equip just that piece. "Revert try-on" hands the character back to Glamourer automation; "End preview" removes the temporary setting too.
- **Apply action:** per baseline and variant, choose exactly which slots an apply touches and which of the mod's pieces each one equips (or none). "Apply" then enables the mod and equips exactly that, no guessing and no emotes. Shared over IPC as `applyAction`.
- **Make Glamourer design:** one button builds a Glamourer design from a baseline or variant: the chosen pieces, fitting hair / face, and the mod with its settings attached as a mod association. "Open in Glamourer" takes you to it.
- **Hair, face, tail and eye mods:** customization entries ("Au Ra Female Hair 194") are chips; the ones made for your race and gender are clickable and switch you to that hairstyle, face or tail through Glamourer, once, unsaved. Entries for other races are greyed out with the reason. Eye packs get an "Eye colour…" picker (right / left / both) so a pack can be judged without opening Glamourer.
- **Collections tab:** read-only overview of your collections, who is assigned to them and how many mods each enables.
- Vitrine never changes your collections, assignments or mod folders. Previews are temporary settings only and are removed when the plugin unloads.

## Commands
- `/vit` opens or closes the window.
- `/vit rescan` rescans changed mods; `/vit rescan all` rescans everything.
- `/vit clear` removes every preview setting Vitrine wrote.

## Works with Lamina
Vitrine exposes its designs over Dalamud IPC (`docs/IPC.md`): Lamina lists them in its Vitrine Design layer and layers the chosen design's mod settings into its own collection. Vitrine does not need Lamina, and Lamina works without Vitrine; installing both just connects them.

One of ahadev's plugins. Install through the custom repository `https://raw.githubusercontent.com/AHAKuo/ahadevtools/main/pluginmaster.json`.
