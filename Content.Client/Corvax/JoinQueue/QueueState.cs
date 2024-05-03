using Content.Shared.Corvax.JoinQueue;
using Robust.Client.Audio;
using Robust.Client.Console;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.Player;

namespace Content.Client.Corvax.JoinQueue;

public sealed class QueueState : State
{
    [Dependency] private readonly IUserInterfaceManager _userInterface = default!;
    [Dependency] private readonly IClientConsoleHost _console = default!;


    // TODO Make this the admin new player notification sound or something
    private const string JoinSoundPath = "/Audio/Effects/voteding.ogg";

    private QueueGui? _gui;


    protected override void Startup()
    {
        _gui = new QueueGui();
        _userInterface.StateRoot.AddChild(_gui);

        _gui.QuitPressed += OnQuitPressed;
    }

    protected override void Shutdown()
    {
        _gui!.QuitPressed -= OnQuitPressed;
        _gui.Dispose();

        Ding();
    }


    public void OnQueueUpdate(QueueUpdateMessage msg)
    {
        _gui?.UpdateInfo(msg.Total, msg.Position);
    }

    private void OnQuitPressed()
    {
        _console.ExecuteCommand("quit");
    }


    private void Ding()
    {
        if (IoCManager.Resolve<IEntityManager>().TrySystem<AudioSystem>(out var audio))
            audio.PlayGlobal(JoinSoundPath, Filter.Local(), false);
    }
}
