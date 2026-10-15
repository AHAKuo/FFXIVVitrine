# Vitrine IPC (consumed by Lamina)

Vitrine is a standalone Dalamud plugin. Lamina discovers it through these Dalamud IPC call gates and treats it as an
optional integration, exactly like Honorific or Moodles. Payloads that carry structure are JSON strings (camelCase,
enums as strings) so both plugins can evolve independently.

| Label | Shape | Meaning |
|---|---|---|
| `Vitrine.ApiVersion` | `Func<(int Major, int Minor)>` | `(1, 0)`. Major changes break the contract. |
| `Vitrine.Ready` | `Action` (event) | Raised after Vitrine finished loading and the IPC is usable. |
| `Vitrine.Disposed` | `Action` (event) | Raised when Vitrine is unloading. |
| `Vitrine.GetDesigns` | `Func<string>` | JSON array of designs (see below), stable order. Empty array when Penumbra is missing. |
| `Vitrine.GetDesign` | `Func<Guid, string>` | JSON object for one design, or `""` when unknown. |
| `Vitrine.DesignsChanged` | `Action` (event) | Raised on the framework thread whenever designs or their settings changed. |
| `Vitrine.OpenWindow` | `Action<bool?>` | `null` toggles, `true` opens, `false` closes the Vitrine window. |
| `Vitrine.PreviewDesign` | `Func<Guid, string>` | Previews the design on the local player (mod + full-set try-on). Returns a status message. |

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
  ]
}
```

- `id` is stable across rescans: auto designs derive it from the mod directory.
- `settings` is `null` when the design uses Penumbra's defaults for the mod.
- `category` is one of Vitrine's category names (`Gear`, `Weapon`, `Accessory`, `Hair`, `Face`, `Body`, `Skin`, `Tail`,
  `Ears`, `Animation`, `Emote`, `Vfx`, `Sfx`, `Ui`, `Minion`, `Mount`, `Housing`, `Npc`, `Other`, `Unknown`).

## Lamina side

`Lamina/Ipc/VitrineBridge.cs` subscribes to these gates, caches the parsed design list, invalidates on
`DesignsChanged`, and exposes `IVitrineBridge` to the layer resolver and the UI. A Lamina pass references a design by
`VitrineDesignLayer.VitrineDesignId`; the resolver turns the design's `mods` into Lamina `ModState` entries, applying the
layer's `SettingsOverride` on top.

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
