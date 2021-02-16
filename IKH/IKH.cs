using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using System.Collections.Generic;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    public class Globals
    {
        public const String Strategy = "IKH";
        public const String SignalSSBSR = "SSB STOP&REVERSE";
    }
    public class Setup
    {
        public bool BreakEvenOnFirstTP;
        public bool BreakEvenOnPipsGain;
        public double BreakEvenPipsGain;

        public bool PartialClosureOnFirstTP;
        public double PartialClosurePerc;

        public bool TrailingStopOnOpen;
        public bool TrailingStopOnFirstTP;
        public bool TrailingStopOnPipsGain;
        public bool TrailingStopOnOpenToBreakEven;
        public Setup() {
        }
    }
    public class Trade
    {
        private List<Position> PositionList;
        public Setup Setup;
        public TradeType Type;
        public double TP1;
        public double TP2;
        public bool TP1Striked;
        public bool TP2Striked;
        public bool BreakEven;
        public double SL;
        public double Volume;
        public string Signal;

        public int PositionCount
        {
            get { return PositionList.Count(); }
        }
        public double PositionEntryPrice {
            get {
                switch(PositionList.Count) {
                    case 1:
                        return PositionList[0].EntryPrice;
                    case 0:
                        return 0;
                    default :
                        return 0;
                }
            }
        }
        public double PositionStopLossPrice {
            get {
                switch (PositionList.Count) {
                    case 1:
                        return PositionList[0].StopLoss.Value;
                    case 0:
                        return 0;
                    default:
                        return 0;
                }
            }
        }
        public Trade(IKH Robot)
        {
            Robot.Positions.Closed += OnClosePosition;
            PositionList = new List<Position>();
            TP1Striked = false;
            TP2Striked = false;
        }
        public bool OpenMarket(IKH Robot)
        {
            TradeResult Result = Robot.ExecuteMarketOrder(Type, Robot.Symbol.Name, Volume, Signal, SL, null, Globals.Strategy,(Setup.TrailingStopOnOpen || Setup.TrailingStopOnOpenToBreakEven));
            //if () {
            //    Result.Position.ModifyTrailingStop(true);
            //}
            if (Result.IsSuccessful)
            {
                PositionList.Add(Result.Position);
                return true;
            }
            return false;
        }
        public void Close()
        {
            foreach (Position x in PositionList)
            {
                x.Close();
            }
        }
        public void SetBreakEven(double pips) {
            foreach(Position x in PositionList) {
                x.ModifyStopLossPips(pips);
            }
            BreakEven = true;
        }
        public void SetTrailingStop(bool activate) {
            foreach (Position x in PositionList) {
                x.ModifyTrailingStop(activate);
            }
        }
        public void ClosePartialPosition() {
            switch (PositionList.Count) {
                case 1:
                    PositionList[0].ModifyVolume(PositionList[0].VolumeInUnits * Setup.PartialClosurePerc);
                    break;
                case 0:
                    break;
                default:
                    break;
            }
            
        }
        public void OnClosePosition(PositionClosedEventArgs e)
        {
            PositionList.RemoveAll(ra => ra.Id == e.Position.Id);
        }
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class IKH : Robot
    {
        [Parameter("Stop Loss Minimo", Group = "Parametri Strategia", DefaultValue = 10.0, MinValue = 1)]
        public double MinSL { get; set; }
        [Parameter("BreakEven Pips", Group = "Parametri Strategia", DefaultValue = 1.0, MinValue = 0.01)]
        public double BEPips { get; set; }
        [Parameter("Volume", Group = "Parametri Strategia", DefaultValue = 10000, MinValue = 0.1)]
        public double Volume { get; set; }
        [Parameter("Buy", Group = "Parametri Strategia", DefaultValue = true)]
        public bool Buy { get; set; }
        [Parameter("Sell", Group = "Parametri Strategia", DefaultValue = true)]
        public bool Sell { get; set; }
        [Parameter("Tenkan Sen", Group = "Periodi Ichimoku", DefaultValue = 9, MinValue = 2)]
        public int Tenkan { get; set; }
        [Parameter("Kijun Sen", Group = "Periodi Ichimoku", DefaultValue = 26, MinValue = 2)]
        public int Kijun { get; set; }
        [Parameter("Senkou Span B", Group = "Periodi Ichimoku", DefaultValue = 52, MinValue = 2)]
        public int Senkou { get; set; }

        public IchimokuKinkoHyo IKHIndicator;


        //orientamento del prezzo (sopra o sotto la Senkou Span B)
        private TradeType LastDirection;
        private List<Trade> TradeList;


        protected override void OnStart()
        {
            IKHIndicator = Indicators.IchimokuKinkoHyo(Tenkan, Kijun, Senkou);
            TradeList = new List<Trade>();
        }
        protected override void OnTick()
        {
            bool TP1Striked = false;
            //gestione della posizione
            foreach(Trade x  in TradeList) {
                if (!x.TP1Striked && x.Type == TradeType.Buy && Symbol.Bid >= PricePlusPips(x.PositionEntryPrice, x.TP1)) {
                    x.TP1Striked = true;
                    TP1Striked = true;
                }
                if (!x.TP1Striked && x.Type == TradeType.Sell && Symbol.Bid <= PricePlusPips(x.PositionEntryPrice, x.TP1 * -1)) {
                    x.TP1Striked = true;
                    TP1Striked = true;
                }
                if (x.Setup.BreakEvenOnFirstTP && TP1Striked) {
                    x.SetBreakEven(BEPips);
                }
                if (x.Setup.TrailingStopOnFirstTP && TP1Striked) {
                    x.SetTrailingStop(true);
                }
                if (x.Setup.PartialClosureOnFirstTP && TP1Striked) {
                    x.ClosePartialPosition();
                }
                if (!x.BreakEven && x.Setup.TrailingStopOnOpenToBreakEven ) {
                    if (x.Type == TradeType.Buy && x.PositionStopLossPrice >= PricePlusPips(x.PositionEntryPrice, BEPips)) {
                        x.SetTrailingStop(false);
                        x.BreakEven = true;
                    } else if (x.Type == TradeType.Sell && x.PositionStopLossPrice <= PricePlusPips(x.PositionEntryPrice, BEPips * -1)) {
                        x.SetTrailingStop(false);
                        x.BreakEven = true;
                    }
                }
            }
        }
        protected override void OnBar()
        {
            //direzione del prezzo
            TradeType Direction;
            //prezzo di take profit
            double TP1Price;
            //prezzo di take profit 2
            double TP2Price;
            //prezzo di StopLoss
            double SLPrice;
            //prezzo del livello senkou Span A
            double SPA = IKHIndicator.SenkouSpanA.Last(26 + 1);
            //prezzo del livello senkou Span B
            double SPB = IKHIndicator.SenkouSpanB.Last(26 + 1);
            //prezzo di chiusura barra corrente
            double Close = Bars.Last(1).Close;
            //variabili temporanee
            double tmpTP1;
            double tmpSL;
            //*************************************************
            //stabilisco la direzione attuale del prezzo
            //*************************************************
            if (Close > SPB)
            {
                Direction = TradeType.Buy;
            }
            else
            {
                Direction = TradeType.Sell;
            }

            //********************
            //SEGNALE REVERSE LONG
            //********************
            // se c'è stato un cambio di direzione del prezzo ed ora è in buy e la kumo era ribassista
            if (SPB > SPA && Direction != LastDirection && Direction == TradeType.Buy)
            {
                // chiudo l'eventuale SHORT aperto
                foreach (Trade x in TradeList.Where(c => c.Signal == Globals.SignalSSBSR && c.PositionCount > 0 && c.Type == TradeType.Sell))
                {
                    x.Close();
                }
                // se non ho già il LONG aperto per questo segnale allora apro il LONG
                if (Buy && !TradeList.Exists(e => e.Signal == Globals.SignalSSBSR && e.Type == TradeType.Buy && e.PositionCount > 0))
                {
                    ;
                    //SLPrice = SPA - (Symbol.Spread * 2);
                    SLPrice = IKHIndicator.SenkouSpanA.Minimum(26 * 2) - (Symbol.Spread * 2);
                    TP1Price = Close + Math.Abs(Symbol.Bid - SLPrice);
                    tmpSL = Math.Max(DistanceInPips(SLPrice), MinSL);
                    tmpTP1 = DistanceInPips(TP1Price);
                    Trade trade = new Trade(this) {
                        Type = TradeType.Buy,
                        Volume = Volume,
                        SL = tmpSL,
                        TP1 = tmpTP1,
                        Signal = Globals.SignalSSBSR,
                        Setup = new Setup() {
                            TrailingStopOnOpenToBreakEven= true,
                            PartialClosureOnFirstTP = true,
                            PartialClosurePerc = 0.5,
                            BreakEvenOnFirstTP = true
                        }
                    };
                    if (trade.OpenMarket(this))
                    {
                        TradeList.Add(trade);
                    }
                    else
                    {
                        Print("Errore Apertura Posizione Long");
                    }
                }
            }
            //********************
            //SEGNALE REVERSE SHORT
            //********************
            // se c'è stato un cambio di direzione del prezzo ed ora  in sell e la kumo era rialzista
            else if (SPB < SPA && Direction != LastDirection && Direction == TradeType.Sell)
            {
                //chiudo l'eventuale LONG
                foreach (Trade x in TradeList.Where(c => c.Signal == Globals.SignalSSBSR && c.PositionCount > 0 && c.Type == TradeType.Buy))
                {
                    x.Close();
                }
                // se non ho già lo SHORT aperto per questo segnale allora apro lo SHORT
                if (Sell && !TradeList.Exists(e => e.Signal == Globals.SignalSSBSR && e.Type == TradeType.Sell && e.PositionCount > 0))
                {
                    //SLPrice = SPA + (Symbol.Spread * 2);
                    SLPrice = IKHIndicator.SenkouSpanA.Maximum(26 * 2) + (Symbol.Spread * 2);
                    TP1Price = Close - Math.Abs(Symbol.Bid - SLPrice);
                    
                    tmpSL = Math.Max(DistanceInPips(SLPrice), MinSL);
                    tmpTP1 = DistanceInPips(TP1Price);
                    Trade trade = new Trade(this) 
                    {
                        Type = TradeType.Sell,
                        Volume = Volume,
                        SL = tmpSL,
                        TP1 = tmpTP1,
                        Signal = Globals.SignalSSBSR,
                        Setup = new Setup() {
                            TrailingStopOnOpenToBreakEven=true,
                            PartialClosureOnFirstTP=true,
                            PartialClosurePerc=0.5,
                            BreakEvenOnFirstTP = true

                        }
                    };

                    if (trade.OpenMarket(this))
                    {
                        TradeList.Add(trade);
                    }
                    else
                    {
                        Print("Errore Apertura Posizione Short");
                    }
                }
            }
            LastDirection = Direction;
        }
        protected override void OnStop()
        {

        }
        public double DistanceInPips(double price)
        {
            double delta = Math.Abs(Symbol.Bid - price);
            double pips = delta / Symbol.PipSize;
            return Math.Round(pips, 0);
        }
        public double PricePlusPips(double price, double pips) {
            return price + (pips * Symbol.PipValue);
        }
    }
}
