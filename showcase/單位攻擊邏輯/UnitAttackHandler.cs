using BattleCore.Core;
using BattleCore.Core.Utility;
using BattleCore.NewTypeGames.AttackHandler.TargetSelectStrategy;
using BattleCore.NewTypeGames.Buff;
using BattleCore.NewTypeGames.Buff.Adder;
using BattleCore.NewTypeGames.Buff.Divider;
using BattleCore.NewTypeGames.Buff.TriggerHandler;
using BattleCore.NewTypeGames.Command;
using BattleCore.NewTypeGames.Command.Receiver;
using BattleCore.NewTypeGames.Command.Receiver.BuilderUtility;
using BattleCore.NewTypeGames.Condition;
using BattleCore.NewTypeGames.DamageFormula.Factory;
using BattleCore.NewTypeGames.Delegate;
using BattleCore.NewTypeGames.FlowEvent.Args;
using BattleCore.NewTypeGames.Order;
using BattleCore.NewTypeGames.Unit;
using BattleCore.NewTypeGames.Utility.DamageHistory;
using BattleCore.NewTypeGames.Utility.TargetSelectHistory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BattleCore.NewTypeGames.AttackHandler
{
    public class UnitAttackHandler : UnitAttackHandlerAbstact
    {
        //private
        private Skill.SkillData currentSkillData;
        private List<Unit.Unit> targetList;

        //inject
        readonly ICommandInvoker commandWaiter;
        readonly BattleCoreEvent<AGOrder> orderInvoker;
        readonly IFactory<StrategyType, string, int, List<string>, ITargetSelectStrategy> strategyFactory;
        readonly IBuffAdder buffAdder;
        readonly BuffTriggerHandler buffTriggerHandler;
        readonly BattleCore.EnergyIncreaseRateSetting energySetting;
        readonly IBuilderUtility builderUtility;
        readonly IFactory<ConditionData, string, string, ICondition> conditionFactory;
        readonly IFactory<DamageFormulaParam, IDamageFormula> damageFormulaFactory;
        readonly ILogger logger;
        readonly DamageHistoryAbstract damageHistory;
        readonly TargetSelectHistoryAbstract targetSelectHistoryOnAttack;
        readonly IsAutoBattle isAutoBattle;
        readonly List<string> manualInsertUnitList;
        readonly FlowEvent.Subject.FlowEventCenterAbstract unitAttackEvent;

        private static Random rd = new Random(Guid.NewGuid().GetHashCode());
        private bool isEnergySkill = false;

        //TODO 可以做為外部選擇目標，
        readonly List<string> speciticTarget = new List<string>();



        public UnitAttackHandler(
            IUnitsHandler _handler, 
            Unit.Unit _attacker,
            IBuffAdder _buffAdder,
            BuffTriggerHandler _buffTriggerHandler,
            ICommandInvoker _cmdInvoker,
            BattleCoreEvent<AGOrder> _oi,
            BattleCore.EnergyIncreaseRateSetting _energySetting,
            IFactory<StrategyType, string, int, List<string>, ITargetSelectStrategy> _factory,
            IBuilderUtility _bu,
            IFactory<ConditionData, string, string, ICondition> _condionFactory,
            IFactory<DamageFormulaParam, IDamageFormula> _damageFormula,
            ILogger _logger,
            DamageHistoryAbstract _damageHistory,
            TargetSelectHistoryAbstract _tsha,
            List<string>  _specTarget,
            IsAutoBattle _isAutoBattle,
            List<string> _manualInsertUnitList,
            FlowEvent.Subject.FlowEventCenterAbstract _flowEvent) : base(_handler, _attacker)
        {
            commandWaiter = _cmdInvoker;
            orderInvoker = _oi;
            buffAdder = _buffAdder;
            buffTriggerHandler = _buffTriggerHandler;
            strategyFactory = _factory;
            energySetting = _energySetting;
            builderUtility = _bu;
            conditionFactory = _condionFactory;
            damageFormulaFactory = _damageFormula;
            logger = _logger;
            damageHistory = _damageHistory;
            targetSelectHistoryOnAttack = _tsha;
            speciticTarget = _specTarget;
            isAutoBattle = _isAutoBattle;
            manualInsertUnitList = _manualInsertUnitList;
            unitAttackEvent = _flowEvent;

            if (attacker.InCrowdControl == false)
            {
                SetCurrentSkill();
                SelectTargets();
            }
        }

        public override void DoDamage()
        {
            var orderTargetList = new List<TargetData>();

            var targetIDList = new List<string>();
            if (targetList != null)
            {
                targetIDList = targetList.Select(x => x.Data.ID).ToList();
            }

            targetSelectHistoryOnAttack.Push(attacker.Data.ID, targetIDList);

            //觸發攻擊者攻擊前的Buff效果
            buffTriggerHandler.Trigger(attacker.Data.ID, Timing.BeforDamage);

            if (attacker.InCrowdControl == false && attacker.HP > 0)
            {
                buffTriggerHandler.Trigger(attacker.Data.ID, Timing.BeforeDamageWithCC);

                //掛載buff至目標對象 before damage
                IBuffDivider divider = new BuffDivider(
                    attacker,
                    currentSkillData,
                    targetList.Select(x => x.Data.ID).ToList(),
                    buffAdder,
                    Timing.BeforDamage,
                    strategyFactory,
                    conditionFactory);
                divider.Divide();

                Dictionary<string, int> damageRecore = new Dictionary<string, int>();

                foreach (var uni in targetList)
                {
                    
                    //設定資料， 一定要在最開始就把資料設定好，不然觸發Buff時會有問題，這裡有順序依賴。
                    attacker.CurrentAttackTarget = uni;
                    uni.WhoWillAttack = attacker;

                    //觸發Buff。 Buff必須先觸發，接下來再計算傷害的時候才會把觸發的數值計算進去。
                    //只有目標跟攻擊者不同陣營的時候，才會觸發此時機
                    if (attacker.Data.UnitFaction != uni.Data.UnitFaction)
                        buffTriggerHandler.Trigger(uni.Data.ID, Timing.GotDamageBefore);

                    //傷害條件判斷，每個對象都分別判斷傷害
                    var param = currentSkillData.DamageFormulaCondition;
                    ICondition condition = conditionFactory.Create(param, uni.Data.ID, attacker.Data.ID);
                    bool isTrigger = condition.IsMet();

                    //利用條件判斷使用哪種傷害公式
                    string damageFormula = currentSkillData.DefaultDamageFormula;
                    if (isTrigger) damageFormula = currentSkillData.ConditionDamageFormula;
                    if (string.IsNullOrEmpty(damageFormula))
                    {
                        if(currentSkillData.Type == Skill.SkillType.Damage) damageFormula = "(A_ATK - B_DEF) * SR";
                        else damageFormula = "A_ATK * SR";
                    }

                    IDamageFormula targetFormula = damageFormulaFactory.Create(
                        new DamageFormulaParam {
                            AttackerID = attacker.Data.ID,
                            GotDamagerID = uni.Data.ID,
                            CurrentSkillDmgRate = currentSkillData.DmgRate,
                            Formula = damageFormula
                        });

                    float finalDamageRate = 1f;
                    float dmgReduceRate = (1 - uni.DamageReduceRate);
                    if (attacker.DamageRateFormula != null) finalDamageRate = attacker.DamageRateFormula.CalculateDamage();

                    //補血的時候，屬性加成倍率以及減傷倍率都強制是一倍
                    if (currentSkillData.Type == Skill.SkillType.Heal)
                    {
                        finalDamageRate = 1f;
                        dmgReduceRate = 1f;
                    }
                    float fdamage = targetFormula.CalculateDamage() * finalDamageRate * dmgReduceRate;
                    int idamage = Math.Max(1, (int)fdamage);

                    //skill rate 為 0 的時候，沒有傷害。
                    if (currentSkillData.DmgRate == 0) idamage = 0;

                    logger.Log("final damage rate : " + finalDamageRate + ", reduce rate : " + dmgReduceRate);

                    //實際扣寫
                    if (currentSkillData.Type == Skill.SkillType.Heal)
                    {
                        var hfd = idamage * (1 + uni.HealingRate);
                        idamage = (int)hfd * -1;
                    }
                    uni.HP -= idamage;

                    //trigger effect on dead
                    if (uni.HP <= 0)
                    {
                        buffTriggerHandler.Trigger(uni.Data.ID, Timing.Dead);
                        buffTriggerHandler.Trigger(attacker.Data.ID, Timing.EnemyDead);

                        //對被打的人的所有同陣營發送同伴死亡事件
                        var targetAlliance = unitsHandler.GetAllUnits().Where(x => x.Data.UnitFaction == uni.Data.UnitFaction && x.Data.ID != uni.Data.ID).ToList();
                        targetAlliance = targetAlliance.Where(x => x.HP > 0).ToList();
                        foreach (var teammate in targetAlliance)
                        {
                            buffTriggerHandler.Trigger(teammate.Data.ID, Timing.AnyAllianceDead);
                        }

                    }
                    else
                    {
                        //增加能量
                        if (currentSkillData.Type == Skill.SkillType.Damage)
                        {
                            uni.Energy += energySetting.OnGotHit;
                            var args = new AfterAddEnergyEventArgs { AddEnergy = energySetting.OnGotHit, Target = uni.Data.ID};
                            if (unitAttackEvent != null) unitAttackEvent.SendFlowEvent(args);
                        }
                    }

                    damageRecore.Add(uni.Data.ID, idamage);
                }

                damageHistory.Push(attacker.Data.ID, damageRecore);

                //掛載buff至目標對象 after damage
                IBuffDivider divider2 = new BuffDivider(
                    attacker,
                    currentSkillData,
                    targetList.Select(x => x.Data.ID).ToList(),
                    buffAdder,
                    Timing.AfterDamage,
                    strategyFactory,
                    conditionFactory);

                divider2.Divide();

                //觸發攻擊者攻擊後的Buff效果
                buffTriggerHandler.Trigger(attacker.Data.ID, Timing.AfterDamage);

                //觸發受擊者計算傷害後的受擊效果
                foreach (var uni in targetList)
                {
                    //trigger effect on got damage
                    //只有目標跟攻擊者不同陣營的時候，才會觸發此時機
                    if (attacker.Data.UnitFaction != uni.Data.UnitFaction)
                        buffTriggerHandler.Trigger(uni.Data.ID, Timing.GotDamageAfter);
                }

                //建置目標資料列表
                foreach (var uni in targetList)
                {
                    int idamage = 1;
                    damageRecore.TryGetValue(uni.Data.ID, out idamage);

                    string msg1 = attacker.Data.UnitFaction + " " + attacker.Data.ID + " attack.  HP : " + attacker.HP + ". Use Skill: " + currentSkillData.ID;
                    string msg2 = uni.Data.UnitFaction + " " + uni.Data.ID + ", got Damage : " + idamage + ", HP : " + uni.HP + ", AGI : " + (uni.AdditionAGI + uni.Data.Status.AGI) +  ", Energy : " + uni.Energy;
                    Console.WriteLine(msg1);
                    Console.WriteLine(msg2);
                    Console.WriteLine("================================");
                    var data = new TargetData();
                    data.Buffs = uni.BuffDict;
                    data.Damage = idamage;
                    data.HPBalance = idamage * -1;
                    data.HP = uni.HP;
                    data.MaxHP = uni.MaxHP;
                    data.Energy = uni.Energy;
                    data.MaxEnergy = uni.Data.Status.MaxEnergy;
                    data.ID = uni.Data.ID;
                    data.IsDead = uni.HP <= 0;
                    data.InCrowdControl = uni.InCrowdControl;
                    orderTargetList.Add(data);
                }


            }
            else Console.WriteLine(attacker.Data.ID + " is in crowdcontrol");

            //計算完傷害後，不論是否被控場都增加能量。
            if (isEnergySkill == false)
            {
                attacker.Energy += energySetting.OnAttack;
                var args = new AfterAddEnergyEventArgs { AddEnergy = energySetting.OnAttack, Target = attacker.Data.ID };
                if (unitAttackEvent != null) unitAttackEvent.SendFlowEvent(args);
            }

            //觸發行動結束必定觸發的效果
            buffTriggerHandler.Trigger(attacker.Data.ID, Timing.AfterUnitActionEnd);

            var builder = new UnitsActionCMDBuilder(
                unitsHandler,
                attacker, 
                currentSkillData.ID,
                orderTargetList,
                orderInvoker,
                buffTriggerHandler.TriggerdBuffsID,
                builderUtility);
            ICommand cmd = new BuildUnitsActionCMD(builder);
            commandWaiter.AddCommand(cmd);
        }

        private void SetCurrentSkill()
        {
            isEnergySkill = false;

            //Do some select skill logic here;
            if (attacker.InsertSkill == true)
            {
                currentSkillData = attacker.ForceSkill;
                attacker.InsertSkill = false;
            }
            else
            {
                currentSkillData = Skill.SkillData.DefaultSkillData;
                try
                {
                    var energySkill = attacker.GetEnergySkillData();

                    //避免資料異常
                    if (energySkill.CostEnergy <= 0)
                    {
                        BattleCore.Instance.Logger.Log(string.Format("energy skill id : {0},  cost energy shounld not be zero.", energySkill.ID));

                        //強制改為100f
                        energySkill.CostEnergy = 100f;  
                    }

                    bool autoSelectEnergySkill = true;
                    if (attacker.Data.UnitFaction == UnitData.Faction.Player)
                    {
                        if (isAutoBattle() == false)
                        {
                            autoSelectEnergySkill = false;
                            if (manualInsertUnitList != null && manualInsertUnitList.Contains(attacker.Data.ID))
                            {
                                manualInsertUnitList.Remove(attacker.Data.ID);
                                autoSelectEnergySkill = true;
                            }
                        }
                    }

                    if (attacker.Energy >= energySkill.CostEnergy && attacker.CanUseEnergySkill && autoSelectEnergySkill)
                    {
                        currentSkillData = energySkill;
                        attacker.Energy = 0f;
                        isEnergySkill = true;
                    }
                    else currentSkillData = attacker.Data.SkillDatas.ElementAtOrDefault(0);
                }
                catch (Exception e)
                {
                    BattleCore.Instance.Logger.Log(string.Format("[exception] {0}, {1}", e.Message, e.StackTrace));
                }
                
            }
            Debug.Assert(currentSkillData.ID != int.MaxValue);
        }

        //TODO debug 反擊時，可能會有目標已經死亡，但已經先加入排序清單的狀況
        //目前方法，在目標選擇策略上做處理，若目標為上一個攻擊自己的人，且上一個攻擊自己的人已經死亡，
        //則隨機挑選一個非死亡目標
        private void SelectTargets()
        {
            StrategyType type = StrategyType.AllRandom;
            try
            {
                bool isDefine = Enum.IsDefined(typeof(StrategyType), currentSkillData.TargetSelectStrategy);
                if(isDefine)
                    type = (StrategyType)Enum.Parse(typeof(StrategyType), currentSkillData.TargetSelectStrategy);
            }
            catch (Exception e)
            {
                BattleCore.Instance.Logger.Log(e.StackTrace);
            }
            

            ITargetSelectStrategy ts = strategyFactory.Create(
                type, 
                attacker.Data.ID, 
                currentSkillData.TargetCount,
                speciticTarget);
            targetList = ts.GetTargets();
            if (targetList.Count <= 0) BattleCore.Instance.Logger.Log("target list should not be null");
        }

        public struct TargetData
        {
            public string ID { get; set; }
            public int Damage { get; set; }
            public int HP { get; set; }
            public int MaxHP { get; set; }
            public float Energy { get; set; }
            public float MaxEnergy { get; set; }
            public Dictionary<string, IBuff> Buffs { get; set; }
            public bool IsDead { get; set; }
            public bool InCrowdControl { get; set; }
            public int HPBalance { get; set; }          //更動的HP(其實就是damage乘-1。因為負的damage實際上是補血，比較難理解)
        }
    }
}
