# Compile-check stubs

These are not the game. They are hand-written signatures for the handful of
Valheim and BepInEx members TarkoValheim touches, so the mod can be
type-checked on a machine without Valheim installed (CI, cloud sessions):

    dotnet build src/TarkoValheim/TarkoValheim.csproj -p:UseStubs=true

A clean stub build means the code agrees with these signatures and with the
real Jotunn, Harmony and Unity assemblies from NuGet. It does not prove the
signatures match the shipped game: only a build against a real Valheim
install does that. Members confirmed from Jotunn 2.30.2's own IL references
to assembly_valheim are marked `// seen in Jotunn`.
