﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace CasualRacer.Model
{
    internal class Game : INotifyPropertyChanged
    {
        private GameState state;
        const int MAXLAPCOUNT=45; //TODO: mehr Runden zum debuggen  / in die Optionen packen und anpaßbar machen
        const float MAXSTEERANGLE=110;

        /// <summary>
        /// Ruft den <see cref="Track"/> ab.
        /// </summary>
        public Track Track { get; }

        /// <summary>
        /// Gibt den aktuellen <see cref="GameState"/> zurück.
        /// </summary>
        public GameState State
        {
            get { return state; }
            private set
            {
                state = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("State"));
            }
        }

        public TimeSpan CountDown { get; private set; }

        /// <summary>
        /// Ruft den <see cref="Player"/> 1 ab.
        /// </summary>
        public Player Player1 { get; }

        /// <summary>
        /// Ruft den <see cref="Player"/> 2 ab.
        /// </summary>
        public Player Player2 { get; }

        public Game(Track track)
        {
            Track = track;
            State = GameState.CountDown;
            CountDown = TimeSpan.FromSeconds(3);

            Vector goal = (Vector)Track.GoalPosition;

            Vector startOffset1 = default(Vector);
            Vector startOffset2 = default(Vector);

            float startRotation = 0f;

            switch (Track.GetTileByIndex((int)goal.X, (int)goal.Y))
            {
                case TrackTile.GoalDown:
                    startOffset1 = new Vector(0.75f, 0.25f);
                    startOffset2 = new Vector(0.25f, 0.25f);
                    startRotation = 180f;
                    break;
                case TrackTile.GoalLeft:
                    startOffset1 = new Vector(0.75f, 0.75f);
                    startOffset2 = new Vector(0.75f, 0.25f);
                    startRotation = -90f;
                    break;
                case TrackTile.GoalRight:
                    startOffset1 = new Vector(0.25f, 0.25f);
                    startOffset2 = new Vector(0.25f, 0.75f);
                    startRotation = 90f;
                    break;
                case TrackTile.GoalUp:
                    startOffset1 = new Vector(0.25f, 0.75f);
                    startOffset2 = new Vector(0.75f, 0.75f);
                    break;
            }

            Player1 = new Player()
            {
                Position = (Point)((goal + startOffset1) * Track.CELLSIZE),
                Direction = startRotation
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Aktualisiert den Spielstatus.
        /// </summary>
        /// <param name="totalTime">Die total abgelaufene Zeit.</param>
        /// <param name="elapsedTime">Die abgelaufene Zeit seit dem letzten Update.</param>
        public void Update(TimeSpan totalTime, TimeSpan elapsedTime)
        {
            switch (State)
            {
                case GameState.CountDown:
                    CountDown -= elapsedTime;
                    if (CountDown < TimeSpan.Zero)
                    {
                        CountDown = TimeSpan.Zero;
                        State = GameState.Race;
                    }
                    break;
                case GameState.Race:
                    UpdatePlayer(totalTime, elapsedTime, Player1);
                    if (Player1.Lap == MAXLAPCOUNT)
                        State = GameState.Finished;
                    break;
            }
        }

        /// <summary>
        /// Aktualisiert den gegebenen Spieler.
        /// </summary>
        /// <param name="totalTime">Die total abgelaufene Zeit.</param>
        /// <param name="elapsedTime">Die abgelaufene Zeit seit dem letzten Update.</param>
        /// <param name="player">Der Spieler.</param>
        private void UpdatePlayer(TimeSpan totalTime, TimeSpan elapsedTime, Player player)
        {
            player.TotalTime += elapsedTime;
            player.LapTime += elapsedTime;

            // Lenkung
            if (player.SteerLeft)
                player.Direction -= (float)elapsedTime.TotalSeconds * MAXSTEERANGLE;
            if (player.SteerRight)
                player.Direction += (float)elapsedTime.TotalSeconds * MAXSTEERANGLE;

            // Beschleunigung & Bremse
            var targetSpeed = 0f;
            if (player.Acceleration)
                targetSpeed = 100f;
            if (player.Break)
                targetSpeed = -50f;

            // Anpassung je nach Untergrund
            targetSpeed *= Track.GetSpeedByPosition(player.Position);

            // Beschleunigung
            if (targetSpeed > player.Velocity)
            {
                player.Velocity += 80f * (float)elapsedTime.TotalSeconds;
                player.Velocity = Math.Min(targetSpeed, player.Velocity);
            }
            else if (targetSpeed < player.Velocity)
            {
                player.Velocity -= 100f * (float)elapsedTime.TotalSeconds;
                player.Velocity = Math.Max(targetSpeed, player.Velocity);
            }

            // Positionsveränderung
            var direction = (float)(player.Direction * Math.PI) / 180f;
            var velocity = new Vector(
                Math.Sin(direction) * player.Velocity * elapsedTime.TotalSeconds,
                -Math.Cos(direction) * player.Velocity * elapsedTime.TotalSeconds);
            player.Position += velocity;

            // Goal State ermitteln
            var playerCell = Track.GetTileByPosition((Point)player.Position);
            
            if (playerCell.IsGoalTile())
            {
                Debug.Write("goal tile: "+playerCell);
                switch (playerCell)
                {
                    case TrackTile.GoalDown:
                        player.PositionRelativeToGoal = (player.Position.Y % Track.CELLSIZE < Track.CELLSIZE / 2) ? PlayerPositionRelativeToGoal.BeforeGoal : PlayerPositionRelativeToGoal.AfterGoal;
                        break;
                    case TrackTile.GoalLeft:
                        player.PositionRelativeToGoal = (player.Position.X % Track.CELLSIZE >= Track.CELLSIZE / 2) ? PlayerPositionRelativeToGoal.BeforeGoal : PlayerPositionRelativeToGoal.AfterGoal;
                        break;
                    case TrackTile.GoalRight:
                        player.PositionRelativeToGoal = (player.Position.X % Track.CELLSIZE < Track.CELLSIZE / 2) ? PlayerPositionRelativeToGoal.BeforeGoal : PlayerPositionRelativeToGoal.AfterGoal;
                        break;
                    case TrackTile.GoalUp:
                        Debug.WriteLine(player.Position.Y);
                        player.PositionRelativeToGoal = (player.Position.Y % Track.CELLSIZE >= Track.CELLSIZE / 2) ? PlayerPositionRelativeToGoal.BeforeGoal : PlayerPositionRelativeToGoal.AfterGoal;
                        break;
                }
            }
            else {
                
                player.PositionRelativeToGoal = PlayerPositionRelativeToGoal.AwayFromGoal;
            }
        }

        public enum GameState
        {
            CountDown,
            Race,
            Finished
        }
    }
}
