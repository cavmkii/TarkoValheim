# Compile-check stubs

These are not the game. They are hand-written signatures for the Valheim,
BepInEx and Unity UI (uGUI) members TarkoValheim touches, so the mod can be
type-checked on a machine without Valheim installed (CI, cloud sessions):

    dotnet build src/TarkoValheim/TarkoValheim.csproj -p:UseStubs=true

A clean stub build means the code agrees with these signatures and with the
real Jotunn, Harmony and Unity assemblies from NuGet. It does not prove the
signatures match the shipped game: only a build against a real Valheim
install does that. The header of `assembly_valheim/Valheim.cs` names the sources the
Valheim signatures were checked against.
