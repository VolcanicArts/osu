// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Spectator;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Replays.Types;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;

namespace osu.Game.Screens.Play
{
    public class SpectatorPlayer : Player
    {
        [Resolved]
        private SpectatorClient spectatorClient { get; set; }

        private readonly Score score;

        protected override bool CheckModsAllowFailure() => false; // todo: better support starting mid-way through beatmap

        public SpectatorPlayer(Score score)
        {
            this.score = score;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            spectatorClient.OnUserBeganPlaying += userBeganPlaying;

            AddInternal(new OsuSpriteText
            {
                Text = $"Watching {score.ScoreInfo.User.Username} playing live!",
                Font = OsuFont.Default.With(size: 30),
                Y = 100,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            spectatorClient.OnNewFrames += userSentFrames;
        }

        private void userSentFrames(int userId, FrameDataBundle bundle)
        {
            if (userId != score.ScoreInfo.User.Id)
                return;

            if (!LoadedBeatmapSuccessfully)
                return;

            if (!this.IsCurrentScreen())
                return;

            foreach (var frame in bundle.Frames)
            {
                IConvertibleReplayFrame convertibleFrame = GameplayRuleset.CreateConvertibleReplayFrame();
                convertibleFrame.FromLegacy(frame, GameplayBeatmap.PlayableBeatmap);

                var convertedFrame = (ReplayFrame)convertibleFrame;
                convertedFrame.Time = frame.Time;

                score.Replay.Frames.Add(convertedFrame);
            }
        }

        protected override ResultsScreen CreateResults(ScoreInfo score)
        {
            return new SpectatorResultsScreen(score);
        }

        protected override void PrepareReplay()
        {
            DrawableRuleset?.SetReplayScore(score);
        }

        protected override GameplayClockContainer CreateGameplayClockContainer(WorkingBeatmap beatmap, double gameplayStart)
        {
            // if we already have frames, start gameplay at the point in time they exist, should they be too far into the beatmap.
            double? firstFrameTime = score.Replay.Frames.FirstOrDefault()?.Time;

            if (firstFrameTime == null || firstFrameTime <= gameplayStart + 5000)
                return base.CreateGameplayClockContainer(beatmap, gameplayStart);

            return new MasterGameplayClockContainer(beatmap, firstFrameTime.Value, true);
        }

        public override bool OnExiting(IScreen next)
        {
            spectatorClient.OnUserBeganPlaying -= userBeganPlaying;
            spectatorClient.OnNewFrames -= userSentFrames;

            return base.OnExiting(next);
        }

        private void userBeganPlaying(int userId, SpectatorState state)
        {
            if (userId != score.ScoreInfo.UserID) return;

            Schedule(() =>
            {
                if (this.IsCurrentScreen()) this.Exit();
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (spectatorClient != null)
            {
                spectatorClient.OnUserBeganPlaying -= userBeganPlaying;
                spectatorClient.OnNewFrames -= userSentFrames;
            }
        }
    }
}
