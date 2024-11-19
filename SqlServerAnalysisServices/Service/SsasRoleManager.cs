using Framework.Common;
using Framework.Model;
using Framework.Service;
using TOM = Microsoft.AnalysisServices.Tabular;

namespace SqlServerAnalysisServices.Service;

internal class SsasRoleManager : ISsasRoleManager
{
    private readonly string _databaseName;
    private readonly Ssas _parent;

    public SsasRoleManager(string databaseName, Ssas parent)
    {
        _databaseName = databaseName;
        _parent = parent;
    }

    public IEnumerable<SsasDatabaseRole> Roles
    {
        get
        {
            using var server = _parent.GetServer();
            var db = server.Databases.FindByName(_databaseName);

            return db.Model.Roles.Select(role => new SsasDatabaseRole
            {
                Name = role.Name,
                Description = role.Description,
                Permission = role.ModelPermission switch
                {
                    TOM.ModelPermission.Administrator => SsasRolePermission.Administrator,
                    TOM.ModelPermission.ReadRefresh => SsasRolePermission.ReadRefresh,
                    _ => SsasRolePermission.Read
                }
            });
        }
    }

    public IEnumerable<SsasRoleMember> AddExternalMembers(string roleName, params string[] memberNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        if (_parent.IsProcessing(_databaseName))
        {
            throw new Exception($"'{_databaseName}' is currently being processed.");
        }

        using var server = _parent.GetServer();
        var db = server.Databases.FindByName(_databaseName);
        var role = db.Model.Roles.Find(roleName) ?? throw new Exception($"Role '{roleName}' does not exist.");

        var result = new List<SsasRoleMember>();

        foreach (var memberName in memberNames)
        {
            if (role.Members.Find(memberName) is not null)
            {
                continue;
            }

            role.Members.Add(new TOM.ExternalModelRoleMember
            {
                MemberName = memberName,
                IdentityProvider = "AzureAD",
                MemberType = TOM.RoleMemberType.Auto
            });

            result.Add(new SsasRoleMember
            {
                IdentityProvider = "AzureAD",
                Name = memberName,
                Role = new SsasDatabaseRole
                {
                    Description = role.Description,
                    Name = role.Name,
                    Permission = role.ModelPermission switch
                    {
                        TOM.ModelPermission.Administrator => SsasRolePermission.Administrator,
                        TOM.ModelPermission.ReadRefresh => SsasRolePermission.ReadRefresh,
                        _ => SsasRolePermission.Read
                    }
                }
            });
        }

        role.Model.SaveChanges();

        return result;
    }

    public void CreateRole(string name, string description, SsasRolePermission permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_parent.IsProcessing(_databaseName))
        {
            throw new Exception($"'{_databaseName}' is currently being processed.");
        }

        using var server = _parent.GetServer();
        var db = server.Databases.FindByName(_databaseName);

        if (db.Model.Roles.ContainsName(name))
        {
            throw new Exception($"Role '{name}' already exists.");
        }

        db.Model.Roles.Add(new TOM.ModelRole
        {
            Name = name,
            Description = description,
            ModelPermission = permission switch
            {
                SsasRolePermission.Administrator => TOM.ModelPermission.Administrator,
                SsasRolePermission.ReadRefresh => TOM.ModelPermission.ReadRefresh,
                _ => TOM.ModelPermission.Read
            }
        });

        db.Model.SaveChanges();
    }

    public void DeleteRole(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_parent.IsProcessing(_databaseName))
        {
            throw new Exception($"'{_databaseName}' is currently being processed.");
        }

        using var server = _parent.GetServer();
        var db = server.Databases.FindByName(_databaseName);

        if (!db.Model.Roles.ContainsName(name))
        {
            throw new Exception($"Role '{name}' does not exist.");
        }

        db.Model.Roles.Remove(name);

        db.Model.SaveChanges();
    }

    public IEnumerable<SsasRoleMember> GetRoleExternalMembers(string roleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        using var server = _parent.GetServer();
        var db = server.Databases.FindByName(_databaseName);
        var role = db.Model.Roles.Find(roleName) ?? throw new Exception($"Role: '{roleName}' does not exist.");

        return role.Members
            .Select(member => new SsasRoleMember
            {
                IdentityProvider = "AzureAD",
                Name = member.Name,
                Role = new SsasDatabaseRole
                {
                    Description = role.Description,
                    Name = role.Name,
                    Permission = role.ModelPermission switch
                    {
                        TOM.ModelPermission.Administrator => SsasRolePermission.Administrator,
                        TOM.ModelPermission.ReadRefresh => SsasRolePermission.ReadRefresh,
                        _ => SsasRolePermission.Read
                    }
                }
            })
            .ToList();
    }

    public int RemoveMembers(string roleName, params string[] memberNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        if (_parent.IsProcessing(_databaseName))
        {
            throw new Exception($"'{_databaseName}' is currently being processed.");
        }

        using var server = _parent.GetServer();
        var db = server.Databases.FindByName(_databaseName);
        var role = db.Model.Roles.Find(roleName);

        var affected = 0;

        if (role is not null)
        {
            foreach (var memberName in memberNames)
            {
                if (role.Members.Find(memberName) is null)
                {
                    throw new Exception($"Member '{memberName}' not found.");
                }

                role.Members.Remove(memberName);
                affected++;
            }

            role.Model.SaveChanges();

            return affected;
        }

        throw new Exception($"Role '{roleName}' does not exist.");
    }

    public void UpdateRole(string name, SsasDatabaseRole updated)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_parent.IsProcessing(_databaseName))
        {
            throw new Exception($"'{_databaseName}' is currently being processed.");
        }

        using var server = _parent.GetServer();
        var db = server.Databases.FindByName(_databaseName);
        var role = db.Model.Roles.Find(name) ?? throw new Exception($"Role '{name}' does not exists."); ;

        role.Model.Description = updated.Description;
        role.ModelPermission = updated.Permission switch
        {
            SsasRolePermission.Administrator => TOM.ModelPermission.Administrator,
            SsasRolePermission.ReadRefresh => TOM.ModelPermission.ReadRefresh,
            _ => TOM.ModelPermission.Read
        };

        db.Model.SaveChanges();
    }
}