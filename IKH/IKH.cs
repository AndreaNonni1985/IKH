using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using System.Collections.Generic;
using cAlgo.Indicators;
using System.Linq;

namespace cAlgo.Robots
{
    public class Trade
    {
        public Position POS;
        //prezzo di take profit
        public double TP1Price;
        //pips di teke profit
        public double TP1;
        //prezzo di take profit
        public double TP2Price;
        //pips di teke profit
        public double TP2;
        //flag TP1 colpito
        public bool TP1Striked;
        //flag TP2 colpito
        public bool TP2Striked;
        //prezzo di StopLoss
        public double SLPrice;
        //pips di StopLoss
        public double SL;

        public Trade()
        {
            TP1Striked = false;
            TP2Striked = false;
        }

        public void DoOrder(IKH Robot)
        {
            TradeResult Result = Robot.ExecuteMarketOrder(TradeType.Buy, Robot.Symbol.Name, Robot.Volume, IKH.Strategy, SL, null, IKH.Strategy);
            if (Result.IsSuccessful)
            {
                POS = Result.Position;
            }
        }
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class IKH : Robot
    {
        public const String Strategy = "IKH";

        [Parameter("Stop Loss Minimo", Group = "Parametri Strategia", DefaultValue = 10.0, MinValue = 1)]
        public double MinSL { get; set; }
        [Parameter("Volume", Group = "Parametri Strategia", DefaultValue = 10000, MinValue = 0.1)]
        public double Volume { get; set; }
        [Parameter("Tenkan Sen", Group = "Periodi Ichimoku", DefaultValue = 9, MinValue = 2)]
        public int Tenkan { get; set; }
        [Parameter("Kijun Sen", Group = "Periodi Ichimoku", DefaultValue = 26, MinValue = 2)]
        public int Kijun { get; set; }
        [Parameter("Senkou Span B", Group = "Periodi Ichimoku", DefaultValue = 52, MinValue = 2)]
        public int Senkou { get; set; }

        public IchimokuKinkoHyo IKHIndicator;

        //prezzo di take profit
        public double TP1Price;
        //pips di teke profit
        public double TP1;
        //prezzo di take profit
        public double TP2Price;
        //pips di teke profit
        public double TP2;
        //flag TP colpito
        public bool ProfitStriked;
        //prezzo di StopLoss
        public double SLPrice;
        //pips di StopLoss
        public double SL;

        //orientamento del prezzo (sopra o sotto la Senkou Span B)
        public TradeType Direction;
        public TradeType LastDirection;
        public List<Trade> TradeList;


        protected override void OnStart()
        {
            IKHIndicator = Indicators.IchimokuKinkoHyo(Tenkan, Kijun, Senkou);
            TradeList = new List<Trade>();
        }
        protected override void OnTick()
        {
            //logica TakeProfit , BreakEven , Trail Stop

            //se il prezzo colpisce il primo profit prendo metà profitto e lascio correre il resto mettendo a BreakEven
            if (Positions.Count == 1 && !ProfitStriked)
            {
                if (Positions[0].TradeType == TradeType.Buy && Symbol.Bid >= TP1Price)
                {
                    Positions[0].ModifyVolume(Positions[0].VolumeInUnits / 2);
                    Positions[0].ModifyStopLossPips(-1);
                    Positions[0].ModifyTrailingStop(true);
                    ProfitStriked = true;


                }
                else if (Positions[0].TradeType == TradeType.Sell && Symbol.Bid <= TP1Price)
                {
                    Positions[0].ModifyVolume(Positions[0].VolumeInUnits / 2);
                    Positions[0].ModifyStopLossPips(-1);
                    Positions[0].ModifyTrailingStop(true);
                    ProfitStriked = true;
                }
            }
        }
        protected override void OnBar()
        {
            //prezzo del livello senkou Span A
            double SPA = IKHIndicator.SenkouSpanA.Last(Kijun + 2);
            //prezzo del livello senkou Span B
            double SPB = IKHIndicator.SenkouSpanB.Last(Kijun + 2);
            //prezzo di apertura barra corrente
            double Open = Bars.Last(1).Open;
            //prezzo di apertura barra precedente
            double OpenBefore = Bars.Last(2).Open;
            //prezzo di chiusura barra corrente
            double Close = Bars.Last(1).Close;
            //osservatore execute order
            TradeResult Result;

            //
            //stabilisco al direzione attuale del prezzo
            //
            if (Close > SPB)
            {
                Direction = TradeType.Buy;
            }
            else
            {
                Direction = TradeType.Sell;
            }
            //
            //gestisco lo stop di chiusura candela
            //
            //if (Positions.Count == 1) {
            //    if (Positions[0].TradeType == TradeType.Buy) {
            //        Positions[0].Close();
            //    }
            //    else if (Positions[0].TradeType == TradeType.Sell) {

            //    }
            //}


            //if (SPB > SPA && Close >= Open && Close > SPB && Math.Min(Open, OpenBefore) <= SPB) {
            if (SPB > SPA && Direction != LastDirection && Direction == TradeType.Buy)
            {
                //if (Direction != LastDirection && Direction == TradeType.Buy) {
                // caso in cui la KUMO ribassista viene tagliata al rialzo dal prezzo 

                //stop and reverse
                if (Positions.Count == 1)
                {
                    if (Positions[0].TradeType == TradeType.Sell)
                    {
                        Positions[0].Close();
                    }
                }
                if (Positions.Count == 0)
                {

                    SLPrice = SPA - (Symbol.Spread * 2);
                    TP1Price = Close + Math.Abs(Symbol.Bid - SLPrice);
                    SL = Math.Max(DistanceInPips(SLPrice), MinSL);
                    TP1 = DistanceInPips(TP1Price);

                    ExecuteMarketOrder(TradeType.Buy, Symbol.Name, Volume, Strategy, SL, null, Strategy);
                    ProfitStriked = false;


                    //Trade trade = new Trade() {
                    //    SLPrice = SPA - (Symbol.Spread * 2),
                    //    TP1Price = Close + Math.Abs(Symbol.Bid - SLPrice),
                    //    SL = Math.Max(DistanceInPips(SLPrice), MinSL),
                    //    TP1 = DistanceInPips(TP1Price)
                    //};
                    //trade.DoOrder(this);
                    //TradeList.Add(trade);


                }
                //} else if (SPA > SPB && Close >= Open && Close > SPB && Math.Min(Open, OpenBefore) <= SPB) {
                //} else if(SPB < SPA && Close <= Open && Close < SPB && Math.Max(Open, OpenBefore) >= SPB) {
            }
            else if (SPB < SPA && Direction != LastDirection && Direction == TradeType.Sell)
            {
                //else if (Direction != LastDirection && Direction == TradeType.Sell) {
                // caso in cui la KUMO rialzista viene tagliata al ribasso dal prezzo 

                //stop and reverse
                if (Positions.Count == 1)
                {
                    if (Positions[0].TradeType == TradeType.Buy)
                    {
                        Positions[0].Close();
                    }
                }
                if (Positions.Count == 0)
                {
                    SLPrice = SPA + (Symbol.Spread * 2);
                    TP1Price = Close - Math.Abs(Symbol.Bid - SLPrice);
                    SL = Math.Max(DistanceInPips(SLPrice), MinSL);
                    TP1 = DistanceInPips(TP1Price);

                    Result = ExecuteMarketOrder(TradeType.Sell, Symbol.Name, Volume, Strategy, SL, null, Strategy);

                    ProfitStriked = false;
                }
            }
            LastDirection = Direction;
        }
        public double DistanceInPips(double price)
        {
            double delta = Math.Abs(Symbol.Bid - price);
            double pips = delta / Symbol.PipSize;
            return Math.Round(pips, 0);
        }
        protected override void OnStop()
        {

        }
    }
}
