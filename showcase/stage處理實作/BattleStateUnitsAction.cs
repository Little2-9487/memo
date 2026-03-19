using BattleCore.NewTypeGames.Delegate;
using BattleCore.NewTypeGames.Other.InsertEnergySkill;
using System.Collections.Generic;
using System.Linq;

namespace BattleCore.NewTypeGames.BattleState
{
    public class BattleStateUnitsAction : BattleStateAbstract
    {
        //private readonly IsAutoBattle isAutoBattle;
        private readonly InsertEnergySkillAbstract insertEnergySkill;

        public BattleStateUnitsAction(BattleCore _core, InsertEnergySkillAbstract _insertEnergySkill) : base(_core)
        {
            //isAutoBattle = _isAutoBattle;
            insertEnergySkill = _insertEnergySkill;
        }

        public override void BuildOrder()
        {
            //每個角色行動前都額外再次檢查是否有能量滿的角色可以加入行動序列
            if (insertEnergySkill != null) insertEnergySkill.InsertEnergySkill();

            //取出順序角色
            Unit.Unit attacker = core.SequenceHandler.GetNext();
            if (attacker == null)
            {
                core.Logger.Log("attacker should not be null!!");
            }

            if (attacker != null)
            {
                core.SetAttackInfo(attacker);

                //攻擊結束，輸出指令
                core.DoDamage();

                //每次行動都把沒HP的角色排出序列
                core.SequenceHandler.TidyUp();

                //每個角色行動後，也再次檢查是否有能量滿的角色可以加入行動序列
                if (insertEnergySkill != null) insertEnergySkill.InsertEnergySkill();

            }

            
            if (core.SequenceHandler.Peek() == null) core.SetState(core.RoundEnd);
            if (core.GetEnemyRestHP() <= 0 || core.GetPlayerRestHP() <= 0) core.SetState(core.RoundEnd);
            core.CommandWaiter.ExcuteFirstCommand();

        }
    }
}
