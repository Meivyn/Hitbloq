using Hitbloq.Entries;

namespace Hitbloq.Interfaces
{
	internal interface IDifficultyBeatmapUpdater
	{
		public void DifficultyBeatmapUpdated(BeatmapKey beatmapKey, HitbloqLevelInfo? levelInfoEntry);
	}
}