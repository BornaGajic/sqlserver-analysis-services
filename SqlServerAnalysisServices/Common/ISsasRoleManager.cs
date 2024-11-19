using Framework.Model;

namespace Framework.Common;

public interface ISsasRoleManager
{
    IEnumerable<SsasDatabaseRole> Roles { get; }

    /// <summary>
    /// Adds external (Azure) member to the role.
    /// </summary>
    IEnumerable<SsasRoleMember> AddExternalMembers(string roleName, params string[] memberNames);

    void CreateRole(string name, string description, SsasRolePermission permission);

    void DeleteRole(string name);

    IEnumerable<SsasRoleMember> GetRoleExternalMembers(string roleName);

    int RemoveMembers(string roleName, params string[] memberNames);

    public void UpdateRole(string name, SsasDatabaseRole updated);
}