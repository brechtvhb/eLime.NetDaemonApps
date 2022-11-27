using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace eLime.NetDaemonApps.Tests.Helpers;

public class HaContextMockNSub
{
    public HaContextMockNSub()
    {
        HaContext = Substitute.For<IHaContext>();
        HaContext.StateAllChanges().Returns(
            StateChangeSubject
        );
        HaContext.StateChanges().Returns(
            StateChangeSubject.Where(n => n.New?.State != n.Old?.State)
        );
    }

    public IHaContext HaContext { get; init; }
    public Subject<StateChange> StateChangeSubject { get; } = new();
}