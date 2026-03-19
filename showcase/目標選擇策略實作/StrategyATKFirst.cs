using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore.NewTypeGames.Unit;

namespace BattleCore.NewTypeGames.AttackHandler.TargetSelectStrategy
{
    public class StrategyATKFirst : StrategySelectAbstract
    {

        public StrategyATKFirst(UnitData.Faction _faction, IUnitsHandler _unitsList, int _targetCount) : base(_unitsList, _targetCount, _faction)
        {
            
        }

        public override List<Unit.Unit> GetTargets()
        {
            List<Unit.Unit> rs = new List<Unit.Unit>();
            try
            {
                //先取出活人
                var aliveTarget = unitsList.GetAllUnits().Where(x => x.Data.UnitFaction == targetFaction && x.HP > 0).ToList();

                //照攻擊排序
                aliveTarget = aliveTarget.OrderByDescending(x => x.Data.Status.ATK).ToList();

                //依照攻擊數量取出目標
                rs = aliveTarget.Take(targetCount).ToList();
            }
            catch (Exception e)
            {
                BattleCore.Instance.Logger.Log(e.StackTrace);
            }
            return rs;
        }
    }
}
