## Shuffle behaviour

The Randomize button is the only thing that turns shuffle on. It does two things at once: it starts a random
playable tape, and it keeps picking a random tape each time a track ends.

Play is not shuffle-aware. From a stopped deck it starts the first tape; while a tape is playing it pauses; while
paused it resumes.

Double-clicking a tape plays that tape and leaves shuffle exactly as it was:

- shuffle was **on**: the tape you picked plays, and the random pick resumes when that track ends
- shuffle was **off**: it stays off
- **Stop** turns shuffle off, so after a stop both Play and double-click start with shuffle off

Shuffle also switches itself off if a random pick finds nothing playable, for example an empty deck or tapes the
audio manager cannot load.
