using Vokasia.Api.Security;
using Vokasia.Domain.Common;

namespace Vokasia.Tests.Guard;

public sealed class TeacherPlacementScopeTests
{
    [Fact]
    public void TeacherMayAccessOnlyCurrentPlacementAssignment()
    {
        var teacherId = Guid.NewGuid();
        var otherTeacherId = Guid.NewGuid();

        Assert.True(TeacherPlacementScope.CanAccess(UserRole.Teacher, teacherId, teacherId));
        Assert.False(TeacherPlacementScope.CanAccess(UserRole.Teacher, teacherId, otherTeacherId));
        Assert.True(TeacherPlacementScope.CanAccess(UserRole.TenantAdmin, teacherId, otherTeacherId));
        Assert.True(TeacherPlacementScope.CanAccess(UserRole.DeptHead, teacherId, otherTeacherId));
    }
}
