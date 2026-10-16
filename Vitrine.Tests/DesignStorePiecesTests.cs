using Glamourer.Api.Enums;
using Newtonsoft.Json.Linq;
using Vitrine;
using Vitrine.Designs;
using Vitrine.Ipc;
using Vitrine.TryOn;
using Xunit;

namespace Vitrine.Tests;

public class DesignStorePiecesTests
{
    private sealed class FakeResolver : IItemResolver
    {
        private readonly Dictionary<string, EquippableItem> _items = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Cow Top"]   = new(1001, "Cow Top", ApiEquipSlot.Body),
            ["Cow Legs"]  = new(1002, "Cow Legs", ApiEquipSlot.Legs),
            ["Cow Boots"] = new(1003, "Cow Boots", ApiEquipSlot.Feet),
        };

        public bool TryResolve(string name, out EquippableItem item)
            => _items.TryGetValue(name, out item);
    }

    private static ModInformation Cowkini()
        => new()
        {
            ModDirectory  = "Cowkini",
            Name          = "Cowkini",
            Type          = VitrineCategory.Gear,
            AffectedItems = { new(VitrineCategory.Gear, "Cow Top", null), new(VitrineCategory.Gear, "Cow Legs", null), new(VitrineCategory.Gear, "Cow Boots", null) },
        };

    [Fact]
    public void Designs_ExportResolvedPieces_FilteredByTheirSlots()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vitrine-{Guid.NewGuid():N}.json");
        try
        {
            var store = new VitrineDesignStore(path, _ => { }, new FakeResolver());
            store.Load();
            store.UpdateFromCatalog(new[] { Cowkini() });

            var auto = store.AutoDesignFor("Cowkini")!;
            Assert.Equal(new[] { ApiEquipSlot.Body, ApiEquipSlot.Legs, ApiEquipSlot.Feet }, auto.Pieces!.Keys);
            Assert.Equal(1001UL, auto.Pieces[ApiEquipSlot.Body].ItemId);

            var bodyOnly = new ApplyAction();
            bodyOnly.Slots[ApiEquipSlot.Body] = new ApplySlot { Enabled = true, ItemId = 1001, ItemName = "Cow Top" };
            bodyOnly.Slots[ApiEquipSlot.Legs] = new ApplySlot { Enabled = false, ItemId = 1002, ItemName = "Cow Legs" };
            var variantId = store.CreateVariant("Cowkini", "Body only");
            store.UpdateVariant(variantId, applyAction: bodyOnly);

            var variant = store.Get(variantId)!;
            Assert.Equal(new[] { ApiEquipSlot.Body }, variant.Pieces!.Keys);

            var json = JObject.Parse(DesignJson.Serialize(variant));
            Assert.Equal("Cow Top", json["pieces"]!["Body"]!["itemName"]!.Value<string>());
            Assert.Null(json["pieces"]!["Legs"]);
            Assert.Equal(1001, json["pieces"]!["Body"]!["itemId"]!.Value<long>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WithoutResolver_PiecesAreNull()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vitrine-{Guid.NewGuid():N}.json");
        try
        {
            var store = new VitrineDesignStore(path, _ => { });
            store.Load();
            store.UpdateFromCatalog(new[] { Cowkini() });
            Assert.Null(store.AutoDesignFor("Cowkini")!.Pieces);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void HairMods_ExportTheirCustomizations_ForAutoDesignsAndVariants()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vitrine-{Guid.NewGuid():N}.json");
        try
        {
            var hair = new ModInformation
            {
                ModDirectory  = "Hair194",
                Name          = "Hair 194",
                Type          = VitrineCategory.Hair,
                AffectedItems = { new(VitrineCategory.Hair, "Customization: Midlander Female Hair 194", null), new(VitrineCategory.Hair, "Customization: Au Ra Female Hair 194", null) },
            };
            var store = new VitrineDesignStore(path, _ => { });
            store.Load();
            store.UpdateFromCatalog(new[] { Cowkini(), hair });

            Assert.Empty(store.AutoDesignFor("Cowkini")!.Customizations!);
            var auto = store.AutoDesignFor("Hair194")!;
            Assert.Equal(new[] { ModelRace.Midlander, ModelRace.AuRa }, auto.Customizations!.Select(c => c.Race));
            Assert.All(auto.Customizations, c => Assert.Equal("Hairstyle", c.Key));

            var variant = store.Get(store.CreateVariant("Hair194", "Copy"))!;
            Assert.Equal(2, variant.Customizations!.Count);
            Assert.Equal(194, variant.Customizations[1].Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ActiveDesign_IsStoredPerMod_AndSurvivesReload()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vitrine-{Guid.NewGuid():N}.json");
        try
        {
            var store = new VitrineDesignStore(path, _ => { });
            store.Load();
            store.UpdateFromCatalog(new[] { Cowkini() });
            var auto = store.AutoDesignFor("Cowkini")!;
            Assert.Null(store.ActiveDesignFor("Cowkini"));

            var changed = 0;
            store.Changed += () => changed++;
            store.SetActiveDesign("Cowkini", auto.Id);
            Assert.Equal(auto.Id, store.ActiveDesignFor("cowkini"));
            Assert.Equal(1, changed);

            var reloaded = new VitrineDesignStore(path, _ => { });
            reloaded.Load();
            Assert.Equal(auto.Id, reloaded.ActiveDesignFor("Cowkini"));

            reloaded.SetActiveDesign("Cowkini", null);
            Assert.Null(reloaded.ActiveDesignFor("Cowkini"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
