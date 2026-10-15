using Glamourer.Api.Enums;
using Newtonsoft.Json.Linq;
using Vitrine;
using Vitrine.Designs;
using Vitrine.Glamourer;
using Vitrine.Ipc;
using Vitrine.TryOn;
using Xunit;

namespace Vitrine.Tests;

public class ClearOtherSlotsTests
{
    [Fact]
    public void NothingIds_FollowGlamourersEncoding_AndWeaponsHaveNone()
    {
        Assert.Equal(uint.MaxValue - 128 - 3, ApplyPlanner.NothingId(ApiEquipSlot.Head));
        Assert.Equal(uint.MaxValue - 128 - 12, ApplyPlanner.NothingId(ApiEquipSlot.LFinger));
        Assert.Null(ApplyPlanner.NothingId(ApiEquipSlot.MainHand));
        Assert.Null(ApplyPlanner.NothingId(ApiEquipSlot.OffHand));
        Assert.Equal(10, ApplyPlanner.ClearableSlots.Count);
        Assert.DoesNotContain(ApiEquipSlot.MainHand, ApplyPlanner.ClearableSlots);
    }

    [Fact]
    public void SlotsToClear_AreTheClearableSlotsThePiecesDoNotFill()
    {
        var pieces = new[] { new EquippableItem(1, "Top", ApiEquipSlot.Body), new EquippableItem(2, "Sword", ApiEquipSlot.MainHand) };
        var clear  = ApplyPlanner.SlotsToClear(pieces);

        Assert.DoesNotContain(ApiEquipSlot.Body, clear);
        Assert.Contains(ApiEquipSlot.Head, clear);
        Assert.Contains(ApiEquipSlot.RFinger, clear);
        Assert.Equal(9, clear.Count);
    }

    [Fact]
    public void GlamourerDesign_WritesNothingWithApply_ForEmptiedSlots()
    {
        var pieces = new[] { new EquippableItem(1001, "Cow Top", ApiEquipSlot.Body) };
        var design = GlamourerDesignBuilder.Build("X", "", pieces, null, null, null, clearOtherSlots: true);
        var equipment = (JObject)design["Equipment"]!;

        Assert.Equal(1001UL, equipment["Body"]!["ItemId"]!.Value<ulong>());
        Assert.Equal(uint.MaxValue - 128 - 8, equipment["Feet"]!["ItemId"]!.Value<ulong>());
        Assert.True(equipment["Feet"]!["Apply"]!.Value<bool>());
        Assert.Null(equipment["MainHand"]);
        Assert.Null(equipment["OffHand"]);
        Assert.Equal(10, equipment.Count);

        var plain = GlamourerDesignBuilder.Build("X", "", pieces, null, null, null);
        Assert.Single((JObject)plain["Equipment"]!);
    }

    [Fact]
    public void Flag_IsStored_Inherited_AndExported()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vitrine-{Guid.NewGuid():N}.json");
        try
        {
            var store = new VitrineDesignStore(path, _ => { });
            store.Load();
            store.UpdateFromCatalog(new[] { new ModInformation { ModDirectory = "Cowkini", Name = "Cowkini", Type = VitrineCategory.Gear } });
            store.SetBaseline("Cowkini", new Baseline { ClearOtherSlots = true });

            Assert.True(store.AutoDesignFor("Cowkini")!.ClearOtherSlots);
            var variantId = store.CreateVariant("Cowkini", "V");
            Assert.True(store.Get(variantId)!.ClearOtherSlots);
            Assert.True(store.UpdateVariant(variantId, clearOtherSlots: false));
            Assert.False(store.Get(variantId)!.ClearOtherSlots);

            var json = JObject.Parse(DesignJson.Serialize(store.AutoDesignFor("Cowkini")!));
            Assert.True(json["clearOtherSlots"]!.Value<bool>());
            Assert.True(DesignJson.Deserialize(json.ToString())!.ClearOtherSlots);

            var reloaded = new VitrineDesignStore(path, _ => { });
            reloaded.Load();
            reloaded.UpdateFromCatalog(new[] { new ModInformation { ModDirectory = "Cowkini", Name = "Cowkini", Type = VitrineCategory.Gear } });
            Assert.True(reloaded.AutoDesignFor("Cowkini")!.ClearOtherSlots);
            Assert.False(reloaded.Get(variantId)!.ClearOtherSlots);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
