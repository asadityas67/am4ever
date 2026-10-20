// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Profile;

namespace Am4ever.Core.Build;

/// <summary>Loads the profiles and donor adapters that ship with the app, from next to the assembly.</summary>
public static class BundledData
{
    public static BoardProfile LoadProfile(string id) =>
        ProfileLoader.LoadProfile(File.ReadAllBytes(Resolve("profiles", id)));

    public static DonorAdapter LoadAdapter(string id) =>
        ProfileLoader.LoadAdapter(File.ReadAllBytes(Resolve("donors", id)));

    static string Resolve(string folder, string id) => Path.Combine(AppContext.BaseDirectory, folder, id + ".json");
}
