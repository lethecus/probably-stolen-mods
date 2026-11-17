using UnityEngine;

namespace FastCharger
{
	/// <summary>
	/// Drives the rainbow animation from a normal Update instead of ModHelper coroutines.
	/// ModHelper.StartCoroutine/StopAllCoroutines do not exist in older game builds, and calling them
	/// aborted OnEnable there, which left every saved charger without its item definition (the 1x1
	/// placeholder). Update exists on every build, so this path cannot fail that way.
	/// </summary>
	public sealed class FastChargerUpdater : MonoBehaviour
	{
		private void Update()
		{
			FastChargerMod.TickAnimation();
		}
	}
}
