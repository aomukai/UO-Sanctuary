using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Targeting;

namespace Sanctuary.Integration;

public static class WardrobeChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(WardrobeChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "wardrobe-checks.json");
    private sealed record Saved(uint Player, CharacterProfile Profile, ProfessionState Profession, string Skills, uint Robe);
    private sealed class PendingTarget() : Target(2, false, TargetFlags.None);
    public static void Configure()
    {
        Register("wardrobe-check", Check);
        Register("wardrobe-reload-check", Reload);
    }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable craft-only wardrobe checks", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", command); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    private static void Require(bool condition, string message)
    { if (!condition) { throw new InvalidOperationException(message); } }
    private static CharacterProfile Profile(PlayerMobile p)
    { ClassCreation.Profiles.TryGet(p.Serial, out var profile); return profile; }
    private static string Skills(PlayerMobile p) => JsonSerializer.Serialize(new
    {
        p.Skills.Cap,
        Values = Enumerable.Range(0, p.Skills.Length).Select(i => p.Skills[i]).Select(s => new { s.BaseFixedPoint, s.CapFixedPoint, s.Lock })
    });
    private static bool Swap(PlayerMobile p, Item wardrobe, SkillName craft) => WardrobeSwitch.Commit(p, wardrobe,
        TownRecognition.BeginSession(p), Profile(p), ClassProfessions.Profiles.Find(p), Profile(p).Class, craft);
    private static PlayerMobile Player()
    {
        var p = new PlayerMobile { Player = true, Body = 0x190, Name = "WardrobeCheck" };
        ClassCreation.Profiles.Assign(p, DiabloClass.Necromancer); p.AddItem(new Backpack());
        RecognitionChecks.Place(p); return p;
    }
    private static void Check()
    {
        var saved = new System.Collections.Generic.List<Saved>();
        foreach (bool throughCraft in new[] { false, true })
        {
            var p = Player(); string site = RecognitionChecks.Place(p); var wardrobe = TownRecognition.PublicWardrobe(site);
            Require(!Swap(p, wardrobe, SkillName.Tinkering), "Unrecognized character switched craft");
            if (throughCraft) { p.Skills.Cartography.Base = 100; }
            else { ClassCreation.Profiles.AdvanceLevel(p, 80); }
            Require(TownRecognition.Claim(p, site, throughCraft ? RecognitionHonor.Pillar : RecognitionHonor.Hero), "First ceremony failed");
            Require(WardrobeAccess.Ready(p, wardrobe) && !ClassRespec.Ready(p, wardrobe), "First ceremony did not unlock switching alone");
            Require(Swap(p, wardrobe, SkillName.Alchemy) && ClassProfessions.Profiles.Find(p).Confirmed,
                "Earned wardrobe could not confirm the preselected default craft");
            var same = ClassProfessions.Profiles.Find(p);
            Require(Swap(p, wardrobe, SkillName.Alchemy) && ReferenceEquals(same, ClassProfessions.Profiles.Find(p)), "Unchanged selection mutated profession");
            for (int i = 0; i < p.Skills.Length; i++)
            { p.Skills[i].CapFixedPoint = 1100; p.Skills[i].BaseFixedPoint = 125 + i; p.Skills[i].SetLockNoRelay((SkillLock)(i % 3)); }
            var robe = new Robe(); Require(p.EquipItem(robe), "Could not equip robe");
            var pet = new SkeletonGuardian(); pet.Initialize(p, 1, 0); pet.SetControlMaster(p); pet.MoveToWorld(p.Location, p.Map);
            p.Backpack.DropItem(new Gold(1234)); p.BankBox.DropItem(new Gold(4321));
            p.Hits = 12; p.Mana = 8; p.Stam = 10;
            var session = ClassCombat.For(p);
            session.NextAction = Core.TickCount - 1234; session.RecoveryUntil = Core.TickCount - 987;
            p.NextSkillTime = Core.TickCount - 567; p.NextSpellTime = Core.TickCount - 321;
            long action = session.NextAction, recovery = session.RecoveryUntil, skillTime = p.NextSkillTime, spellTime = p.NextSpellTime;
            var profile = Profile(p); var stats = ClassCreation.Profiles.Stats(p.Serial);
            int points = ClassCreation.Profiles.AvailablePoints(p.Serial), str = p.RawStr, dex = p.RawDex, intel = p.RawInt;
            string skills = Skills(p);
            foreach (byte craft in TradeSkills.Crafting)
            {
                Require(Swap(p, wardrobe, (SkillName)craft), $"Craft {craft} could not be selected");
                foreach (byte other in TradeSkills.Crafting)
                { Require(ClassProfessions.Request(p, (SkillName)other) == (craft == other), "Native craft activation disagrees with selection"); }
                foreach (byte gather in TradeSkills.Gathering) { Require(ClassProfessions.Request(p, (SkillName)gather), "Switch disabled gathering"); }
                Require(Skills(p) == skills && Profile(p) == profile && ClassCreation.Profiles.Stats(p.Serial) == stats
                    && ClassCreation.Profiles.AvailablePoints(p.Serial) == points && (p.RawStr, p.RawDex, p.RawInt) == (str, dex, intel)
                    && (p.Hits, p.Mana, p.Stam) == (12, 8, 10) && robe.Parent == p && !pet.Deleted && pet.ControlMaster == p
                    && session.NextAction == action && session.RecoveryUntil == recovery && p.NextSkillTime == skillTime && p.NextSpellTime == spellTime
                    && p.Backpack.GetAmount(typeof(Gold)) == 1234 && p.BankBox.GetAmount(typeof(Gold)) == 4321,
                    "Craft switch changed skills, class, points, pools, gear, summons, timers or gold");
            }
            pet.Delete();
            Guards(p, wardrobe);
            PendingWork(p);
            foreach (int index in Enumerable.Range(0, TownRecognition.Halls.Length))
            {
                site = RecognitionChecks.Place(p, index);
                Require(Swap(p, TownRecognition.PublicWardrobe(site), SkillName.Cartography), "Supported hall could not switch");
            }
            long stale = TownRecognition.BeginSession(p);
            EventSink.InvokeDisconnected(p); EventSink.InvokeConnected(p);
            Require(!WardrobeSwitch.Commit(p, TownRecognition.PublicWardrobe(site), stale, Profile(p), ClassProfessions.Profiles.Find(p),
                Profile(p).Class, SkillName.Imbuing), "Relog accepted stale wardrobe session");
            saved.Add(new(p.Serial.Value, Profile(p), ClassProfessions.Profiles.Find(p), Skills(p), robe.Serial.Value));
        }
        RespecInvalidates();
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
    }
    private static void Guards(PlayerMobile p, Item wardrobe)
    {
        void Reject(string reason) => Require(!Swap(p, wardrobe, SkillName.Alchemy), reason);
        p.Body = 0x192; Reject("Ghost switched craft"); p.Body = 0x190;
        p.Frozen = true; Reject("Frozen character switched craft"); p.Frozen = false;
        p.Paralyzed = true; Reject("Paralyzed character switched craft"); p.Paralyzed = false;
        p.Target = new PendingTarget(); Reject("Pending target switched craft"); p.Target = null;
        p.NextSkillTime = Core.TickCount + 1000; Reject("Skill action switched craft"); p.NextSkillTime = Core.TickCount;
        p.NextSpellTime = Core.TickCount + 1000; Reject("Spell action switched craft"); p.NextSpellTime = Core.TickCount;
        p.BeginAction<CraftSystem>(); Reject("Unfinished crafting switched craft"); p.EndAction<CraftSystem>();
        var session = ClassCombat.For(p);
        session.PendingAbility = CombatAbility.Teeth; Reject("Pending attack switched craft"); session.PendingAbility = CombatAbility.None;
        session.HeldAttack = true; Reject("Held attack switched craft"); session.HeldAttack = false;
        session.NextAction = Core.TickCount + 1000; Reject("Action cooldown switched craft"); session.NextAction = Core.TickCount;
        session.RecoveryUntil = Core.TickCount + 1000; Reject("Recovery switched craft"); session.RecoveryUntil = Core.TickCount;
        var enemy = new Skeleton { Frozen = true }; enemy.MoveToWorld(p.Location, p.Map);
        session.Target = enemy.Serial; Reject("Combat target switched craft"); session.Target = Serial.Zero;
        var pet = new SkeletonGuardian(); pet.Initialize(p, 1, 0); pet.SetControlMaster(p); pet.MoveToWorld(p.Location, p.Map);
        pet.Combatant = enemy; Reject("Fighting follower switched craft"); pet.Delete();
        enemy.AggressiveAction(p); Reject("Recent defender aggression switched craft");
        p.RemoveAggressor(enemy); enemy.RemoveAggressed(p);
        p.AggressiveAction(enemy); Reject("Recent attacker aggression switched craft");
        p.RemoveAggressed(enemy); enemy.RemoveAggressor(p);
        enemy.Delete(); p.Combatant = null;
        Require(Swap(p, wardrobe, SkillName.Alchemy), "Quiet character remained combat locked");
        long token = TownRecognition.BeginSession(p); var profile = Profile(p); var profession = ClassProfessions.Profiles.Find(p);
        TownRecognition.BeginSession(p);
        Require(!WardrobeSwitch.Commit(p, wardrobe, token, profile, profession, profile.Class, SkillName.Imbuing), "Old session accepted");
        token = TownRecognition.BeginSession(p);
        Require(!WardrobeSwitch.Commit(p, wardrobe, token, profile, profession, (DiabloClass)255, SkillName.Imbuing)
            && !WardrobeSwitch.Commit(p, wardrobe, token, profile, profession, profile.Class, SkillName.Mining), "Invalid class/gathering selection accepted");
        var location = p.Location; p.MoveToWorld(new Point3D(p.X + 40, p.Y, p.Z), p.Map); Reject("Remote switch accepted"); p.MoveToWorld(location, p.Map);
        var fake = new PublicRecognitionWardrobe("Felucca/Britain", "Fake"); fake.MoveToWorld(p.Location, p.Map);
        Require(!Swap(p, fake, SkillName.Imbuing), "Unregistered wardrobe accepted"); fake.Delete();
        var portable = new EarnedWardrobe(p); p.Backpack.DropItem(portable);
        Require(!Swap(p, portable, SkillName.Imbuing), "Portable wardrobe accepted"); portable.Delete();
    }
    private static void PendingWork(PlayerMobile p)
    {
        string site = RecognitionChecks.Place(p); var wardrobe = TownRecognition.PublicWardrobe(site);
        Require(Swap(p, wardrobe, SkillName.Tinkering), "Tinkering setup failed");
        FixtureSpace.Place(p,new Point3D(5550,1150,0), Map.Trammel);
        CraftFacilities.Register(new(SkillName.Tinkering,p.Map.Name,p.X+1,p.Y,p.Z,0xB4A));
        p.Skills.Tinkering.SetLockNoRelay(SkillLock.Up); p.Skills.Tinkering.Base = 0;
        var trainer = new ProfessionInstructor(); trainer.MoveToWorld(p.Location, p.Map); trainer.AIObject.AITimer.Stop();
        Require(trainer.Teach(SkillName.Tinkering, p, 0, false), "Trainer quote failed");
        var tool = new TinkerTools(100); p.Backpack.DropItem(tool); p.Backpack.DropItem(new IronIngot(30));
        var recipe = DefTinkering.CraftSystem.CraftItems.SearchFor(typeof(SewingKit));
        var ticket = (CraftTransactions.Request)CraftTransactions.Begin(p, DefTinkering.CraftSystem, recipe, typeof(IronIngot), tool, -1);
        Require(CraftTransactions.Valid(ticket), "Craft ticket invalid before switch");
        RecognitionChecks.Place(p); Require(Swap(p, wardrobe, SkillName.Imbuing) && Swap(p, wardrobe, SkillName.Tinkering), "Switch roundtrip failed");
        p.MoveToWorld(trainer.Location, trainer.Map);
        var payment = new Gold(300);
        Require(Server.Engines.Craft.TradeSkillHooks.TrainingPayment(trainer, p, payment) == false
            && !payment.Deleted && payment.Amount == 300 && p.Skills.Tinkering.Base == 0, "Old training quote charged after switch roundtrip");
        payment.Delete(); trainer.Delete();
        int ingots = p.Backpack.GetAmount(typeof(IronIngot));
        Require(!CraftTransactions.Complete(ticket) && tool.UsesRemaining == 100 && p.Backpack.GetAmount(typeof(IronIngot)) == ingots,
            "Old craft ticket consumed resources after switch roundtrip");
        tool.Delete(); RecognitionChecks.Place(p);
    }
    private static void RespecInvalidates()
    {
        var p = Player(); string site = RecognitionChecks.Place(p); var wardrobe = TownRecognition.PublicWardrobe(site);
        ClassCreation.Profiles.AdvanceLevel(p, 80); p.Skills.Cartography.Base = 100;
        TownRecognition.Claim(p, site, RecognitionHonor.Hero); TownRecognition.Claim(p, site, RecognitionHonor.Pillar);
        ClassCreation.Profiles.TrySpendStat(p, CharacterStat.Vitality); p.Backpack.DropItem(new Gold(5000));
        long token = TownRecognition.BeginSession(p); var profile = Profile(p); var profession = ClassProfessions.Profiles.Find(p);
        Require(ClassRespec.Commit(p, wardrobe, profile, ClassCreation.Profiles.Stats(p.Serial), ClassCreation.Profiles.AvailablePoints(p.Serial)), "Respec failed");
        Require(!WardrobeSwitch.Commit(p, wardrobe, token, profile, profession, profile.Class, SkillName.Imbuing), "Respec allowed a pending switch");
        p.Delete();
    }
    private static void Reload()
    {
        foreach (var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var p = (PlayerMobile)World.FindMobile((Serial)saved.Player);
            Require(Profile(p) == saved.Profile && ClassProfessions.Profiles.Find(p) == saved.Profession && Skills(p) == saved.Skills
                && World.FindItem((Serial)saved.Robe)?.Parent == p && TownRecognition.HasAccess(p), "Reload lost active craft, skills, gear or rights");
            string site = RecognitionChecks.Place(p);
            Require(Swap(p, TownRecognition.PublicWardrobe(site), saved.Profession.Craft), "Reloaded character cannot use public wardrobe");
        }
    }
}
