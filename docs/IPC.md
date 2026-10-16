# Vitrine IPC (consumed by Lamina)

Vitrine is a standalone Dalamud plugin. Lamina discovers it through these Dalamud IPC call gates and treats it as an
optional integration, exactly like Honorific or Moodles. Payloads that carry structure are JSON strings (camelCase,
enums as strings) so both plugins can evolve independently.

| Label | Shape | Meaning |
|---|---|---|
| `Vitrine.ApiVersion` | `Func<(int Major, int Minor)>` | `(1, 4)`. Major changes break the contract; minor additions are marked below. |
| `Vitrine.Ready` | `Action` (event) | Raised after Vitrine finished loading and the IPC is usable. |
| `Vitrine.Disposed` | `Action` (event) | Raised when Vitrine is unloading. |
| `Vitrine.GetDesigns` | `Func<string>` | JSON array of designs (see below), stable order. Empty array when Penumbra is missing. |
| `Vitrine.GetDesign` | `Func<Guid, string>` | JSON object for one design, or `""` when unknown. |
| `Vitrine.DesignsChanged` | `Action` (event) | Raised on the framework thread whenever designs or their settings changed. |
| `Vitrine.OpenWindow` | `Action<bool?>` | `null` toggles, `true` opens, `false` closes the Vitrine window. |
| `Vitrine.PreviewDesign` | `Func<Guid, string>` | Previews the design on the local player: mod enabled temporarily, its `pieces` equipped, fitting hair / face applied, replaced emote played. One preview at a time; closing the Vitrine window ends it. Returns a status message. |
| `Vitrine.ApplyDesign` | `Func<Guid, string>` | (1.1) Like a preview without the emote: mod enabled temporarily on the local player, redraw, exactly its `pieces` equipped. Returns a status message. |

## Design JSON

```json
{
  "id": "a0f95146-309c-544e-9225-4702f2c86cb1",
  "name": "Cowkini",
  "category": "Gear",
  "isAuto": true,
  "baseDesignId": null,
  "mods": [
    {
      "modDirectory": "Cowkini",
      "modName": "Cowkini",
      "enabled": true,
      "priority": 0,
      "settings": { "Cowkini Legs": ["Default Eve Legs"], "Extras": ["Tiger stripes"] }
    }
  ],
  "applyAction": {
    "slots": {
      "Body": { "enabled": true,  "itemId": 1001, "itemName": "Cow Top" },
      "Legs": { "enabled": true,  "itemId": null, "itemName": null },
      "Feet": { "enabled": false, "itemId": 1003, "itemName": "Cow Boots" }
    }
  },
  "pieces": {
    "Body": { "itemId": 1001, "itemName": "Cow Top" }
  },
  "clearOtherSlots": true,
  "customizations": []
}
```

A hair / face / tail / ears mod carries its customization parts instead of pieces:

```json
{
  "id": "6c0c7d3e-1b2a-5e4f-9a8b-7c6d5e4f3a2b",
  "name": "Hair 194",
  "category": "Hair",
  "mods": [ { "modDirectory": "Hair194", "modName": "Hair 194", "enabled": true, "priority": 0, "settings": null } ],
  "applyAction": null,
  "pieces": {},
  "clearOtherSlots": false,
  "customizations": [
    { "race": "Midlander", "gender": 1, "key": "Hairstyle", "value": 194, "name": "Customization: Midlander Female Hair 194" },
    { "race": "AuRa",      "gender": 1, "key": "Hairstyle", "value": 194, "name": "Customization: Au Ra Female Hair 194" }
  ]
}
```

- `id` is stable across rescans: auto designs derive it from the mod directory.
- `settings` is `null` when the design uses Penumbra's defaults for the mod.
- `pieces` (1.2) is what the design equips, resolved by Vitrine: Glamourer slot name → game item, one per affected slot. It is
  the `applyAction` applied to the mod's affected items, or the first piece of every slot the mod affects when `applyAction`
  is `null`. Consumers take equipment from here and need no item lookup; `null` when Vitrine could not resolve items.
  Lamina's Vitrine Design layer puts these into the virtual design's equipment and can mask slots per layer, so two designs
  can be combined (body from one mod, feet from another).
- `clearOtherSlots` (1.3): when true the design empties every gear slot its `pieces` do not fill (head, body, hands, legs, feet,
  ears, neck, wrists, both rings; weapons are never touched) by applying Glamourer's "Nothing" item (`uint.MaxValue - 128 -
  <Penumbra slot index>`). Vitrine does this for previews and exported Glamourer designs; Lamina's Vitrine Design layer does it
  in the virtual design and can override the flag per layer.
- `customizations` (1.4): every hair / face / tail / ears part the design's mod replaces, parsed from Penumbra's affected items
  (`Customization: <Race> <Gender> <Part> N`), for EVERY model race and gender the mod ships (not filtered by the current
  player, so consumers match at evaluation time and need no re-emit on login). Enabling such a mod shows nothing unless the
  character's customize value is the replaced number, so a consumer sets the matching value alongside the mod. `race` is the
  model race name (`Midlander`, `Highlander`, `Elezen`, `Lalafell`, `Miqote`, `Roegadyn`, `AuRa`, `Hrothgar`, `Viera`; clans
  1..16 map to these in pairs after Midlander 1 / Highlander 2), `gender` is 0 male / 1 female, `key` is the Glamourer customize
  key (`Hairstyle`, `Face`, `TailShape`; Viera ears use `TailShape`), `value` the customize value, `name` the affected-item
  name (informational). One entry per race, gender and key (the first the mod lists); entries without a Glamourer key (body /
  skin textures) are left out. Empty for mods without customizations, `null` when Vitrine could not read the mod. Vitrine's
  own previews apply the entry matching the local player; Lamina's Vitrine Design layer does the same in the virtual design
  for the player's clan and gender (off per layer with "Also apply hair / face / tail").
- `applyAction` (1.1) is the user's explicit choice of which slots the design affects and which piece each one equips. `null` means the default: every equipment slot the
  mod affects, first alternative per slot (the same plan the try-on uses). Otherwise `slots` maps a Glamourer slot name
  (`MainHand`, `OffHand`, `Head`, `Body`, `Hands`, `Legs`, `Feet`, `Ears`, `Neck`, `Wrists`, `RFinger`, `LFinger`) to a choice:
  `enabled: false` leaves the slot alone; `enabled: true` with an `itemId` equips that game item (`itemName` is the affected-item
  name for readers that do not resolve ids); `enabled: true` with `itemId: null` is "none" (part of the action, nothing equipped).
  Slots missing from the map are not touched.
- `category` is one of Vitrine's category names (`Gear`, `Weapon`, `Accessory`, `Hair`, `Face`, `Body`, `Skin`, `Tail`,
  `Ears`, `Animation`, `Emote`, `Vfx`, `Sfx`, `Ui`, `Minion`, `Mount`, `Housing`, `Npc`, `Other`, `Unknown`).

## Lamina side

`Lamina/Ipc/VitrineBridge.cs` subscribes to these gates, caches the parsed design list, invalidates on
`DesignsChanged`, and exposes `IVitrineBridge` to the layer resolver and the UI. A Lamina pass references a design by
`VitrineDesignLayer.VitrineDesignId`; the resolver turns the design's `mods` into Lamina `ModState` entries, applying the
layer's `SettingsOverride` on top, equips its `pieces` and sets the `customizations` entry matching the player's clan and gender.

## Shared Glamourer key

Lamina locks the player's Glamourer state with key `0x4C414D49` ("LAMI"). Vitrine's try-on first calls `SetItem` with key 0
and, on `InvalidKey`, retries with that key so previews work while Lamina is active. Lamina's next apply replaces the try-on.

## Lamina IPC (consumed by Vitrine)

Lamina registers these gates so Vitrine can hand the character back after a preview. Vitrine treats Lamina as optional.

| Label | Shape | Meaning |
|---|---|---|
| `Lamina.ApiVersion` | `Func<(int Major, int Minor)>` | `(1, 0)`. |
| `Lamina.Ready` | `Action` (event) | Raised after Lamina finished loading. |
| `Lamina.Disposed` | `Action` (event) | Raised when Lamina is unloading. |
| `Lamina.Reapply` | `Action` | Re-evaluate and re-apply the active design (same as `/lam reapply`); runs on the framework thread. |
| `Lamina.IsEnabled` | `Func<bool>` | Lamina's master toggle. |

Vitrine calls `Lamina.Reapply` after "End preview", "Revert try-on" and "Clear all previews" when its setting
"After ending a preview, ask Lamina to reapply its design" is on (default).
