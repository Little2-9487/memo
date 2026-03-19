//  =================================================================================
//
//  2021/01/06
//  Buff 跟 Effect 的結構需要大調整，才會比較是全面性統一的邏輯。
//  應該所有都是利用 Effect 來進行觸發。
//  技能裡面應該含有的是 Effect List，然後利用 Effect 來進行觸發 Buff 的動作。
//
//  =================================================================================

using BattleCore.Core;
using BattleCore.Core.Utility;
using BattleCore.NewTypeGames.AttackHandler;
using BattleCore.NewTypeGames.AttackHandler.TargetSelectStrategy;
using BattleCore.NewTypeGames.BattleState;
using BattleCore.NewTypeGames.Buff;
using BattleCore.NewTypeGames.Buff.Factory;
using BattleCore.NewTypeGames.Buff.Adder;
using BattleCore.NewTypeGames.Effect;
using BattleCore.NewTypeGames.Effect.Factory;
using BattleCore.NewTypeGames.Order;
using BattleCore.NewTypeGames.Unit;
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore.NewTypeGames.Buff.TriggerHandler;
using BattleCore.NewTypeGames.Buff.RemoveHandler;
using BattleCore.NewTypeGames.Command.Receiver.BuilderUtility;
using BattleCore.NewTypeGames.Condition;
using BattleCore.NewTypeGames.Condition.Factory;
using BattleCore.NewTypeGames.Logger;
using BattleCore.NewTypeGames.DamageFormula.Factory;
using BattleCore.NewTypeGames.Effect.EffectAction;
using BattleCore.NewTypeGames.Utility.DamageHistory;
using BattleCore.NewTypeGames.Utility.TriggeredBuffHistory;
using BattleCore.NewTypeGames.Utility.TargetSelectHistory;
using BattleCore.NewTypeGames.Other.InsertEnergySkill;
using BattleCore.NewTypeGames.Other.BattleVariable;

namespace BattleCore.NewTypeGames
{
    public class BattleCore : BattleCoreAbstract
    {
        public IBattleState BattleStart { get; private set; }
        public IBattleState WaveStart { get; private set; }
        public IBattleState RoundStart { get; private set; }
        public IBattleState UnitsAction { get; private set; }
        public IBattleState RoundEnd { get; private set; }
        public IBattleState WaveEnd { get; private set; }
        public IBattleState BattleEnd { get; private set; }

        //Battle Variable
        public BattleVariableAbstract BattleVariable {get; private set;}

        //玩家隊伍Queue。 目前應該只會有一支隊伍。這邊這樣做只是因為敵人會有波次，對齊敵人隊伍結構，順便保留彈性空間。
        private Queue<List<UnitData>> playerUnitDataQueue;

        //敵人隊伍Queue。所有波次的敵人隊伍。
        private Queue<List<UnitData>> enemyUnitDataQueue;               
        public IUnitsHandler UnitsHandler { get; private set; }
        public IUnitAttackHandler UnitsAttackHandler { get; set; }
        public ISequenceHandler<Unit.Unit> SequenceHandler { get; private set; }
        public ICommandInvoker CommandWaiter { get; private set; }
        public IFactory<ConditionData, string, string, ICondition> ConditionFactory { get; private set; }
        public IFactory<DamageFormulaParam, IDamageFormula> DamagerFormulaFactory { get; private set; }

        //Buff
        public BattleCoreEvent<BuffMaxRoundArgs> BuffMaxRoundEvent;
        public BattleCoreEvent<BuffTriggedArgs> BuffTriggerEvent;
        public IFactory<BuffData, string, string, IBuff> BuffFactory { get; private set; }
        public IFactory<EffectType, EffectParam, IEffect> EffectFactory { get; private set; }
        public IBuffAdder BuffAdder { get; private set; }
        public BuffTriggerHandler AGBuffTriggerHandler { get; private set; }
        public IBuffRemoveHandler BuffRemoveHandler { get; private set; }

        //Order event
        public BattleCoreEvent<AGOrder> OrderInvoker { get; private set; }

        //effect action 效果觸發的行為實作，試著用這樣的方式看看(會有相依循環注入的問題，所以目前都先用CORE取得這兩個實作)。
        public IAttackerInsertAction AttackerInsertAction { get; private set; }
        public IAddBuffAction AddBuffAction { get; private set; }
        public IAttackerInsertAction EnergySkillInsertAction { get; private set; }    //能量滿的時候，為了符合玩家操作，如果是自動戰鬥要把能量滿的角色再次加入攻擊序列。

        //傷害歷史紀錄，可以用來注入給有需要使用的類別, 每次ROUND結束都會清空。
        public DamageHistoryAbstract DamageHistory { get; private set; }

        //攻擊之前選擇的目標紀錄，可以用來注入給有需要使用的類別, 每次ROUND結束都會清空。
        public TargetSelectHistory TargetSelectHistoryOnAttack { get; private set; }

        //觸發過的BUFF的歷史紀錄，每次取得指令就清空。
        public TriggeredBuffHistoryAbstract TriggeredBuffHistory { get; private set; }

        //Utility
        public IBuilderUtility BuiliderUtility { get; private set; }

        //Logger
        public ILogger Logger = new EmptyLogger();
        public static BattleCore Instance;

        //手動施放技能清單
        public List<string> InsertAttackID { get; private set; }

        //
        public InsertEnergySkillAbstract InsertEnergySkillFlow { get; private set; }

        //static
        public Random Rd = new Random(Guid.NewGuid().GetHashCode());

        //private 
        private IFactory<StrategyType, string, int, List<string>, ITargetSelectStrategy> strategyFactory;
        private EnergyIncreaseRateSetting energySetting;
        private List<string> targetList = new List<string>();

        //custom setter and getter
        public bool HaveNextWave{ get { return enemyUnitDataQueue.Count > 0;} }

        //需要外部控制的參數。目前只有bool, 先簡單做。真的要做到好會需要一個資料結構，設定名稱(key)，以及資料型態。
        private Param.AGBoolParamAbstract AGBoolParam;

        //Game Flow Event
        public FlowEvent.Subject.FlowEventCenterAbstract OnAfterAddEnergy = new FlowEvent.Subject.FlowEventCenter(); //所有增加能量行為之後的事件

        public BattleCore(
            BattleCoreEvent<AGOrder> _orderInvoker,
            Queue<List<UnitData>> _playerUnitData,
            Queue<List<UnitData>> _enemyUnitData,
            EnergyIncreaseRateSetting _energySetting,
            List<string> _manualCastEnergySkillList)
        {
            OrderInvoker = _orderInvoker;
            playerUnitDataQueue = _playerUnitData;
            enemyUnitDataQueue = _enemyUnitData;
            energySetting = _energySetting;
            InsertAttackID = _manualCastEnergySkillList;
            Instance = this;

            //null check
            if (playerUnitDataQueue == null) playerUnitDataQueue = new Queue<List<UnitData>>();
            if (enemyUnitDataQueue == null) enemyUnitDataQueue = new Queue<List<UnitData>>();


            //初始化會需要被外部控制的參數
            AGBoolParam = new Param.Implement.AGBoolParam(boolParamDict);
            AGBoolParam.Init();
        }

        //TODO 可以移至建構式，移出Interface
        public override void Init()
        {
            //data
            List<UnitData> battleUnitsData = new List<UnitData>();
            battleUnitsData.AddRange(playerUnitDataQueue.Dequeue());
            battleUnitsData.AddRange(enemyUnitDataQueue.Dequeue());

            //unit list
            UnitsHandler = new UnitsHandler(battleUnitsData);

            //Energy skill
            EnergySkillInsertAction = new BattleCoreInstertEnergySkill(this);
            InsertEnergySkillFlow = new InsertEnergySkillAG(UnitsHandler, InsertAttackID, EnergySkillInsertAction, AGBoolParam.IsAutoBattle, OnStageChange);

            //state
            BattleStart = new BattleStateBattleStart(this);
            WaveStart = new BattleStateWaveStart(this);
            RoundStart = new BattleStateRoundStart(this);
            UnitsAction = new BattleStateUnitsAction(this, InsertEnergySkillFlow);
            RoundEnd = new BattleStateRoundEnd(this);
            WaveEnd = new BattleStateWaveEnd(this);
            BattleEnd = new BattleStateBattleEnd(this);
            curSataus = BattleStart;

            //battle varibale
            BattleVariable = new BattleVariable();

            //triggered buff history
            TriggeredBuffHistory = new TriggeredBuffHistory();

            //damage history
            DamageHistory = new DamageHistory();

            //target select history on attack
            TargetSelectHistoryOnAttack = new TargetSelectHistory();

            //strategy factory
            strategyFactory = new TargetStrategyFactory(UnitsHandler, DamageHistory);

            //damage formula factory
            DamagerFormulaFactory = new AGDamageFormulaFactory(UnitsHandler);

            //command
            CommandWaiter = new CommandInvoker();

            //buff
            BuffMaxRoundEvent = new BattleCoreEvent<BuffMaxRoundArgs>();
            BuffTriggerEvent = new BattleCoreEvent<BuffTriggedArgs>();
            EffectFactory = new EffectFactory(this, UnitsHandler);
            ConditionFactory = new AGConditionFactory(UnitsHandler, this);
            BuffFactory = new AGBuffFactory(EffectFactory, ConditionFactory, BuffMaxRoundEvent, BuffTriggerEvent, strategyFactory);

            BuffAdder = new AGBuffAdder(BuffFactory, UnitsHandler);
            BuffRemoveHandler = new BuffRemoveHandler(BuffMaxRoundEvent, UnitsHandler, BuffAdder);
            AGBuffTriggerHandler = new BuffTriggerHandler(UnitsHandler, BuffRemoveHandler, BuffTriggerEvent, TriggeredBuffHistory);

            //utility
            BuiliderUtility = new BuilderUtility(UnitsHandler, TriggeredBuffHistory);

            //effect Action 會有循環注入的問題，這邊都先直接用 core 取得以下兩個類別
            AttackerInsertAction = new BattleCoreAttackerInsertAction(this);
            AddBuffAction = new BattleCoreAddBuffAction(BuffAdder, strategyFactory, ConditionFactory);

        }

        public override void GetOrder()
        {
            //每次取得指令就清除觸發BUFF紀錄。
            AGBuffTriggerHandler.TriggerdBuffsID.Clear();
            TriggeredBuffHistory.Clear();
            base.GetOrder();
        }

        public override void SetBool(string _key, bool _value)
        {
            AGBoolParam.SetBool(_key, _value);
        }

        public override bool GetBool(string _key)
        {
            return AGBoolParam.GetBool(_key);
        }

        public override List<string> GetBoolParams()
        {
            return AGBoolParam.GetBoolParams();
        }

        public void InitRound()
        {
            //new round state
            RoundStart = new BattleStateRoundStart(this);
        }

        #region 因為Battle Core會是資料匯聚的地方，先將功能都耦合在此
        public void InitSequence()
        {
            SequenceHandler = new SequenceHandlerV2(UnitsHandler.GetAllUnits());
        }

        public int GetEnemyRestHP()
        {
            int hp = 0;
            hp = UnitsHandler.GetAllUnits().Where(x => x.Data.UnitFaction == Unit.UnitData.Faction.Enemy).Sum(x => x.HP);
            return hp;
        }

        public int GetPlayerRestHP()
        {
            int hp = 0;
            hp = hp = UnitsHandler.GetAllUnits().Where(x => x.Data.UnitFaction == Unit.UnitData.Faction.Player).Sum(x => x.HP);
            return hp;
        }

        public void SetAttackInfo(Unit.Unit _attacker)
        {
            UnitsAttackHandler = new UnitAttackHandler(
                UnitsHandler,
                _attacker,
                BuffAdder,
                AGBuffTriggerHandler,
                CommandWaiter,
                OrderInvoker,
                energySetting,
                strategyFactory,
                BuiliderUtility,
                ConditionFactory, 
                DamagerFormulaFactory,
                Logger,
                DamageHistory,
                TargetSelectHistoryOnAttack,
                targetList,
                AGBoolParam.IsAutoBattle,
                InsertAttackID,
                OnAfterAddEnergy);
        }

        public void DoDamage()
        {
            UnitsAttackHandler.DoDamage();
        }

        public void InsertSkill(Unit.Unit _attacker, Skill.SkillData _skill)
        {
            //Console.WriteLine("Insert Counter Attack : " + _attacker.Data.ID + ", skill id : " + _skill.ID);
            _attacker.InsertSkill = true;
            _attacker.ForceSkill = _skill;
            SequenceHandler.InsertToNext(_attacker);
        }

        public List<UnitData> GetNextWaveEnemyData()
        {
            var rs = new List<UnitData>();
            if (enemyUnitDataQueue.Count > 0) rs = enemyUnitDataQueue.Dequeue();
            return rs;
        }

        public void SetNextWaveUnits(List<Unit.Unit> _units)
        {
            UnitsHandler.Init(_units);
        }

        #endregion

        public struct EnergyIncreaseRateSetting
        {
            public float OnAttack { get; set; }
            public float OnGotHit { get; set; }
        }

    }
}
