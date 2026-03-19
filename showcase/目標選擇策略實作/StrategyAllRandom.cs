using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore.NewTypeGames.Unit;
using BattleCore.Core.Utility;

namespace BattleCore.NewTypeGames.AttackHandler.TargetSelectStrategy
{
    public class StrategyAllRandom : StrategySelectAbstract
    {
        readonly bool allFaction = false;

        public StrategyAllRandom(
            IUnitsHandler _unitsList,
            UnitData.Faction _targetFaction,
            int _targetCount,
            bool _isAll) : base(_unitsList, _targetCount, _targetFaction)
        {
            //targetFaction = _targetFaction;
        }

        public override List<Unit.Unit> GetTargets()
        {
            var rs = new List<Unit.Unit>();
            try
            {
                List<Unit.Unit> liveUnits = unitsList.GetAllUnits().Where(x => x.HP > 0).ToList();
                if (allFaction == false)
                {
                    liveUnits = liveUnits.Where(x => x.Data.UnitFaction == targetFaction).ToList();
                }

                liveUnits.Shuffle();

                rs = liveUnits.Take(Math.Min(targetCount, liveUnits.Count)).ToList();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return rs;
        }
    }
}
