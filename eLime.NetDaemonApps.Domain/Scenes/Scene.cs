using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Scenes;

public class Scene : ServiceTarget
{
    public string SceneId { get; init; }
    private readonly IHaContext _haContext;
    public Scene(IHaContext haContext, string sceneId)
    {
        _haContext = haContext;
        SceneId = sceneId;
        EntityIds = new List<string> { sceneId };
    }

    ///<summary>Activate the scene.</summary>
    public void TurnOn(SceneTurnOnParameters data)
    {
        _haContext.CallService("scene", "turn_on", this, data);
    }

    ///<summary>Activate the scene.</summary>
    ///<param name="transition">Transition duration it takes to bring devices to the state defined in the scene.</param>
    public void TurnOn(long? @transition = null)
    {
        _haContext.CallService("scene", "turn_on", this, new SceneTurnOnParameters { Transition = @transition });
    }

}