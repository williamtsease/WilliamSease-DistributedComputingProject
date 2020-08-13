"/_FINALIZED" directory contains a published version of the program. Run the file DistributedSimulator.exe to experiment with the simulator and test the algorithm yourself.
Running the simulator will create one subdirectory for each server, /Server0, /Server1, etc. This will allow you to watch file duplication in real-time as well as holding the output files. It performs a word-count function on any text files in this same directory beginning with "pg-", feel free to remove some of these files or add new ones to change the execution and results of the program. (If all of these text files are removed, the program will obviously do nothing).

"/Assets" directory contains the code for the program, along with other assets used by the Unity project. I have segregated the code used in the map-reduce project from that used in the construction of the simulator:
- /Assets itself contains Master.cs and Worker.cs
- /Assets/Simualtor contains the code used by the simulator: only LinkSimulator.cs, MessageSimulator.cs, and NodeSimulator.cs, and maybe SimulatorManager.cs are really of interest to us; the others are just in-program admin performing specific functions.

This project was created in Unity, so altering the files or tinkering with components will require an intallation of Unity. This shouldn't be necessary for the purposes of examing the code logic, however.

A number of videos are also placed in this directory to demonstrate the simulator in action. They are prefaced "z_" to group them easily together at the end of the directory. These videos were taken on Thursday August 13th, when the "hitching" issue described in the performance note below was still unsolved, and it is shown quite clearly.

PERFORMANCE NOTE: After writing and submitting the report, I discovered that my asynchronous functions were not working properly after all. I'm setting out to get them fixed before the actual due date, but in the meantime the hitch when a worker receives a large task has returned.