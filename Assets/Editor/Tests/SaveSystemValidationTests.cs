using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Negative tests for the save system: a damaged or hand-edited save file must
// always load into a valid state. Run from Window > General > Test Runner > EditMode.
// The developer's own save file is backed up before each test and restored after it.
public class SaveSystemValidationTests
{
    const string Pet = "\"hasPet\":true,\"petId\":\"Beagle\",\"version\":3,";

    string path, backup;
    bool hadSave, hadCorrupt;

    [SetUp]
    public void BackUpRealSave()
    {
        path = SaveSystem.FilePath;
        backup = path + ".test-backup";
        hadSave = File.Exists(path);
        hadCorrupt = File.Exists(path + ".corrupt");
        if (hadSave)
            File.Copy(path, backup, true);
    }

    [TearDown]
    public void RestoreRealSave()
    {
        if (File.Exists(path))
            File.Delete(path);
        if (File.Exists(path + ".tmp"))
            File.Delete(path + ".tmp");
        if (!hadCorrupt && File.Exists(path + ".corrupt"))
            File.Delete(path + ".corrupt");
        if (hadSave)
        {
            File.Copy(backup, path, true);
            File.Delete(backup);
        }
        SaveSystem.Load();
    }

    static SaveData LoadJson(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SaveSystem.FilePath));
        File.WriteAllText(SaveSystem.FilePath, json);
        SaveSystem.Load();
        return SaveSystem.Data;
    }

    // ------------------------------------------------------------ Care Points and stage

    [Test]
    public void NegativeCarePoints_BecomeZero()
    {
        SaveData d = LoadJson("{" + Pet + "\"careTotal\":-500}");
        Assert.AreEqual(0, d.careTotal);
    }

    [Test]
    public void HugeCarePoints_AreCapped()
    {
        SaveData d = LoadJson("{" + Pet + "\"careTotal\":999999999,\"growthStage\":2}");
        Assert.AreEqual(SaveSystem.MaxCareTotal, d.careTotal);
        Assert.AreEqual(2, d.growthStage);
    }

    [TestCase(0, 0)]      // Baby
    [TestCase(199, 0)]
    [TestCase(200, 1)]    // Young
    [TestCase(499, 1)]
    [TestCase(500, 2)]    // Adult
    public void Stage_CannotBeAheadOfCarePoints(int careTotal, int expectedStage)
    {
        SaveData d = LoadJson("{" + Pet + "\"careTotal\":" + careTotal + ",\"growthStage\":2}");
        Assert.AreEqual(expectedStage, d.growthStage);
    }

    // ------------------------------------------------------------ coins and scores

    [Test]
    public void NegativeCoins_BecomeZero()
    {
        SaveData d = LoadJson("{" + Pet + "\"coins\":-100}");
        Assert.AreEqual(0, d.coins);
    }

    [Test]
    public void HugeCoins_AreCapped()
    {
        SaveData d = LoadJson("{" + Pet + "\"coins\":2000000000}");
        Assert.AreEqual(SaveSystem.MaxCoins, d.coins);
    }

    [Test]
    public void InvalidScores_AreRepaired()
    {
        SaveData d = LoadJson("{" + Pet + "\"miniGameIds\":[\"catch\",\"match\",\"\"],\"miniGameBest\":[-5,2000000000]}");
        Assert.AreEqual(2, d.miniGameIds.Count, "the empty game id is dropped");
        Assert.AreEqual(2, d.miniGameBest.Count, "both lists have the same length");
        Assert.AreEqual(0, d.miniGameBest[0]);
        Assert.AreEqual(SaveSystem.MaxMiniGameScore, d.miniGameBest[1]);
    }

    [Test]
    public void NullScoreLists_AreReplaced()
    {
        SaveData d = LoadJson("{" + Pet + "\"miniGameIds\":null,\"miniGameBest\":null}");
        Assert.IsNotNull(d.miniGameIds);
        Assert.IsNotNull(d.miniGameBest);
        Assert.AreEqual(0, MiniGameScores.Best("catch"));
    }

    // ------------------------------------------------------------ damaged or missing file

    [Test]
    public void CorruptedSave_StartsFresh_AndKeepsACopy()
    {
        if (!hadCorrupt && File.Exists(path + ".corrupt"))
            File.Delete(path + ".corrupt");
        SaveData d = LoadJson("this is not json {{{ ###");
        Assert.IsFalse(d.hasPet);
        Assert.AreEqual(0, d.careTotal);
        Assert.IsTrue(File.Exists(path + ".corrupt"), "a .corrupt copy is kept");
    }

    [Test]
    public void MissingSave_LoadsWithoutError()
    {
        if (File.Exists(path))
            File.Delete(path);
        Assert.DoesNotThrow(SaveSystem.Load);
        Assert.IsNotNull(SaveSystem.Data);
    }

    // ------------------------------------------------------------ pet name

    [Test]
    public void PetNameInSave_LosesRichTextTags()
    {
        SaveData d = LoadJson("{" + Pet + "\"petName\":\"<b><size=500>Mochi</size></b>\"}");
        Assert.AreEqual("Mochi", d.petName);
    }

    [Test]
    public void PetNameInSave_IsLimitedInLength()
    {
        SaveData d = LoadJson("{" + Pet + "\"petName\":\"" + new string('A', 300) + "\"}");
        Assert.AreEqual(SaveSystem.MaxPetNameLength, d.petName.Length);
    }

    [TestCase("<b>Mochi</b>", "Mochi")]
    [TestCase("      ", "")]
    [TestCase("ABCDEFGHIJKLMNOPQRSTUVWXYZ", "ABCDEFGHIJKL")]
    public void NamePopup_CleansTypedNames(string typed, string expected)
    {
        var go = new GameObject("NamePopupTest");
        try
        {
            NamePopup popup = go.AddComponent<NamePopup>();
            MethodInfo clean = typeof(NamePopup).GetMethod("Clean", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.AreEqual(expected, (string)clean.Invoke(popup, new object[] { typed }));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    // ------------------------------------------------------------ rewards

    [Test]
    public void NegativeCarePointAward_IsRejected()
    {
        LoadJson("{" + Pet + "\"careTotal\":100}");
        Assert.IsFalse(CarePoints.Add(-10, "test"));
        Assert.AreEqual(100, CarePoints.Total);
    }

    [Test]
    public void OverflowingCarePointAward_IsCapped()
    {
        LoadJson("{" + Pet + "\"careTotal\":100}");
        CarePoints.Add(int.MaxValue, "test");
        Assert.AreEqual(SaveSystem.MaxCareTotal, CarePoints.Total);
    }

    [Test]
    public void CarePointsWithoutAPet_AreRejected()
    {
        LoadJson("{\"hasPet\":false,\"version\":3}");
        Assert.IsFalse(CarePoints.Add(10, "test"));
        Assert.AreEqual(0, CarePoints.Total);
    }

    [Test]
    public void Wallet_RejectsOverspendingAndNegativeAmounts()
    {
        LoadJson("{" + Pet + "\"coins\":10}");
        Assert.IsFalse(Wallet.Spend(500));
        Wallet.Add(-5);
        Assert.AreEqual(10, Wallet.Coins);
    }
}
