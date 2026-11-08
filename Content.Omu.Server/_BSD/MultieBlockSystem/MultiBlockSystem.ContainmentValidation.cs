
using System.Linq;
using System.Numerics;
using Content.Omu.Server._BSD.MultiBlockSystem.Components;
using Robust.Shared.Map;

namespace Content.Omu.Server._BSD.MultiBlockSystem;

public sealed partial class BSDMultiBlockSystem : EntitySystem
{
    /// <summary>
    /// Function that ensure that the given origin point is unable to reach the edge of the structure,
    /// functions primary intend for machines that have hazadros effects if not completly contained and ill effects to happen in case of a breach
    /// TODO: make a future algorithm that is able to detect the proper breach location -> odd shapes lead to interesting results rn
    ///       the algorithem knows the relative position based on the structure position and could be augmented in that way
    /// </summary>
    /// <param name="xOrigin"></param>
    /// <param name="yOrigin"></param>
    /// <param name="blockingLayers"></param>
    /// <param name="compStruct"></param>
    /// <returns></returns>
    public bool ValidateContainment(int xOrigin, int yOrigin, string[] blockingLayers, MultiBlockStructureComponent compStruct)
    {
        bool[,] blockingMap = UnifyBlockingMaps(blockingLayers, compStruct);
        HashSet<Vector2> visitedLocations = new();
        HashSet<Vector2> toVisitLocations = new();
        HashSet<Vector2> edgeLocation = new();
        toVisitLocations.Add(new(xOrigin, yOrigin));
        do
        {
            Vector2 currentLoc = toVisitLocations.First();
            if (ValidatePosInArray((int) currentLoc.Y - 1, (int) currentLoc.X, compStruct.TypePresence2DMapDimentionY, compStruct.TypePresence2DMapDimentionX)
                    && !blockingMap[(int) currentLoc.Y - 1, (int) currentLoc.X] && !visitedLocations.Contains(new((int) currentLoc.Y - 1, (int) currentLoc.X)))
            {
                toVisitLocations.Add(new((int) currentLoc.Y - 1, (int) currentLoc.X));
            }
            if (ValidatePosInArray((int) currentLoc.Y + 1, (int) currentLoc.X, compStruct.TypePresence2DMapDimentionY, compStruct.TypePresence2DMapDimentionX)
                    && !blockingMap[(int) currentLoc.Y + 1, (int) currentLoc.X] && !visitedLocations.Contains(new((int) currentLoc.Y + 1, (int) currentLoc.X)))
            {
                toVisitLocations.Add(new((int) currentLoc.Y + 1, (int) currentLoc.X));
            }
            if (ValidatePosInArray((int) currentLoc.Y, (int) currentLoc.X - 1, compStruct.TypePresence2DMapDimentionY, compStruct.TypePresence2DMapDimentionX)
                    && !blockingMap[(int) currentLoc.Y, (int) currentLoc.X - 1] && !visitedLocations.Contains(new((int) currentLoc.Y, (int) currentLoc.X - 1)))
            {
                toVisitLocations.Add(new((int) currentLoc.Y, (int) currentLoc.X - 1));
            }
            if (ValidatePosInArray((int) currentLoc.Y, (int) currentLoc.X + 1, compStruct.TypePresence2DMapDimentionY, compStruct.TypePresence2DMapDimentionX)
                    && !blockingMap[(int) currentLoc.Y, (int) currentLoc.X + 1] && !visitedLocations.Contains(new((int) currentLoc.Y, (int) currentLoc.X + 1)))
            {
                toVisitLocations.Add(new((int) currentLoc.Y, (int) currentLoc.X + 1));
            }
            visitedLocations.Add(currentLoc);
            toVisitLocations.Remove(currentLoc);
        } while (toVisitLocations.Count > 0);
        for (int iterator = 0; iterator < compStruct.TypePresence2DMapDimentionY; iterator++)
        {
            edgeLocation.Add(new(iterator, 0));
            edgeLocation.Add(new(iterator, compStruct.TypePresence2DMapDimentionX));
        }
        for (int iterator = 0; iterator < compStruct.TypePresence2DMapDimentionX; iterator++)
        {
            edgeLocation.Add(new(0, iterator));
            edgeLocation.Add(new(compStruct.TypePresence2DMapDimentionY, iterator));
        }
        int numEdgelocs = edgeLocation.Count;
        edgeLocation.ExceptWith(visitedLocations);
        if (edgeLocation.Count == numEdgelocs) return true;
        return false;
    }
    private bool ValidatePosInArray(int y, int x, int maxY, int maxX)
    {
        if (y < 0 || y > maxY) return false;
        if (x < 0 || x > maxX) return false;
        return true;
    }
    private bool[,] UnifyBlockingMaps(string[] blockingLayers, MultiBlockStructureComponent compStruct)
    {
        bool[,] returnMap = new bool[compStruct.TypePresence2DMapDimentionY + 1, compStruct.TypePresence2DMapDimentionX + 1];
        for (int iterator3 = 0; iterator3 < compStruct.TypePresence2DMapDimentionY; iterator3++)
        {
            for (int iterator4 = 0; iterator4 < compStruct.TypePresence2DMapDimentionX; iterator4++)
            {
                returnMap[iterator3, iterator4] = false;
            }
        }
        foreach (var iterator in blockingLayers)
        {
            for (int iterator3 = 0; iterator3 < compStruct.TypePresence2DMapDimentionY + 1; iterator3++)
            {
                for (int iterator4 = 0; iterator4 < compStruct.TypePresence2DMapDimentionX + 1; iterator4++)
                {
                    if (compStruct.TypePresence2DMap[iterator][iterator3, iterator4] == null) continue;
                    if (compStruct.TypePresence2DMap[iterator][iterator3, iterator4] == false) continue;
                    returnMap[iterator3, iterator4] = true;
                }
            }
        }
        return returnMap;
    }
}