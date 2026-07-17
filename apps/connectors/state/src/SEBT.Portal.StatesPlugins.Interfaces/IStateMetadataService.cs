using SEBT.Portal.StatesPlugins.Interfaces.Data;

namespace SEBT.Portal.StatesPlugins.Interfaces;

public interface IStateMetadataService : IStatePlugin
{
    Task<StateMetadata> GetStateMetadata();
}