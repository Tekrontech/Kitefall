// Roof trait balance check (upgrade spec 10.5): runs equal-skill bot FFA matches and prints the top-half rate per starting-roof trait.
// Run: dotnet run --project Tools/RoofBalance   (target: every trait 45–55 %)
using System; using System.Linq; using System.Collections.Generic; using Kitefall.Core;
class P { static void Main() {
  var wins = new Dictionary<RoofTrait,(int w,int n)>(); var stats=new Dictionary<RoofTrait,(int cr,int lost,int won,int sn)>();
  for (int m=0;m<120;m++){
    MatchSetup setup = MatchFactory.NewSetup(GameMode.FreeForAll, (ArenaType)(m%6), 1000+m);
    setup.Rules.allowBots = true; setup.UseRoofGraph = true; setup.UseLaunchRitual = true; setup.Rules.matchTime=240f;
    MatchFactory.FillWithBots(setup, 8, BotDifficulty.Medium, "sooti");
    // equal skill: same build for all
    foreach (var ps in setup.Participants){ var l=SpecDefaults.Loadout("guddi","short_ribbon","sooti","lakri"); ps.stack=new List<ParticipantLoadout>{l,l,l}; }
    var sim = new MatchSimulation(setup); var brains = MatchFactory.CreateBrains(setup); var inputs = new KiteInput[8];
    var startRoof = new int[8];
    for (int t=0;t<20000 && !sim.IsOver;t++){ foreach(var b in brains) inputs[b.Self]=b.Think(sim); sim.Step(inputs); if (sim.Phase==MatchPhase.Prep && startRoof[0]==0) for(int i=0;i<8;i++) startRoof[i]=sim.Participants[i].RoofId+1; }
    var res = sim.Result;
    foreach (var pr in res.Participants){
      var roof = sim.Arena.Roofs.Roof(startRoof[pr.Index]-1);
      bool top = pr.Placement <= 4;
      foreach (RoofTrait tr in new[]{RoofTrait.High,RoofTrait.Sheltered,RoofTrait.Upwind,RoofTrait.CrateSpot,RoofTrait.Edge,RoofTrait.Cables,RoofTrait.None}){
        bool has = tr==RoofTrait.None ? roof.Traits==RoofTrait.None : roof.Has(tr);
        if (!has) continue; stats.TryGetValue(tr,out var st); stats[tr]=(st.cr+pr.Crashes,st.lost+pr.ContestsLost,st.won+pr.ContestsWon,st.sn+pr.SelfSnaps); wins.TryGetValue(tr,out var v); wins[tr]=(v.w+(top?1:0),v.n+1);
      }
    }
  }
  foreach (var kv in wins){ var st=stats[kv.Key]; Console.WriteLine($"{kv.Key}: top-half {100.0*kv.Value.w/kv.Value.n:0}% of {kv.Value.n} crash/p {1.0*st.cr/kv.Value.n:0.00} won/p {1.0*st.won/kv.Value.n:0.00} lost/p {1.0*st.lost/kv.Value.n:0.00} snap {1.0*st.sn/kv.Value.n:0.00}");}
}}
