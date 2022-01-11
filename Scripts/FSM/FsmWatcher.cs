﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HutongGames.PlayMaker;

namespace HKTool.FSM
{
    
    public class FsmWatcher : WatcherBase<FsmWatcher, PlayMakerFSM, FSMPatch>
    {
        static FsmWatcher()
        {
            On.PlayMakerFSM.Start += PlayMakerFSM_Start;
        }
        public FsmWatcher(IFilter<PlayMakerFSM> filter, WatchHandler<FSMPatch> handler) : base(filter, handler)
        {

        }
        protected override void CallHandler(PlayMakerFSM pm)
        {
            using (var p = pm.Fsm.CreatePatch()) Handler(p);
        }
        private static void PlayMakerFSM_Start(On.PlayMakerFSM.orig_Start orig, PlayMakerFSM self)
        {
            orig(self);
            Test(self);
        }
    }
}