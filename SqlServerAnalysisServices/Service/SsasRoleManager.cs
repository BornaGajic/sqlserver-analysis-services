using SqlServerAnalysisServices.Common;
using SqlServerAnalysisServices.Model;
using System.Runtime.Caching;
using TOM = Microsoft.AnalysisServices.Tabular;

namespace SqlServerAnalysisServices.Service;

internal class SsasRoleManager : ISsasRoleManager
{
    private const int CacheDurationMinutes = 5;
    private static readonly MemoryCache _cache = new MemoryCache($"{nameof(SsasRoleManager)}");
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
            var cacheKey = RolesCacheKey();
            var roles = _cache.Get(cacheKey) as IEnumerable<SsasDatabaseRole>;

            if (roles is null)
            {
                using var server = _parent.GetServer();
                var db = server.Databases.FindByName(_databaseName);

                roles = db.Model.Roles.Select(role => new SsasDatabaseRole
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

                _cache.Set(cacheKey, roles, DateTime.Now.AddMinutes(CacheDurationMinutes));
            }

            return roles;
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
        _cache.Remove(RoleExternalMembersCacheKey(roleName));

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
        _cache.Remove(RolesCacheKey());
    }

    public void DeleteRole(string roleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        if (_parent.IsProcessing(_databaseName))
        {
            throw new Exception($"'{_databaseName}' is currently being processed.");
        }

        using var server = _parent.GetServer();
        var db = server.Databases.FindByName(_databaseName);

        if (!db.Model.Roles.ContainsName(roleName))
        {
            throw new Exception($"Role '{roleName}' does not exist.");
        }

        db.Model.Roles.Remove(roleName);

        db.Model.SaveChanges();
        _cache.Remove(RolesCacheKey());
    }

    public IEnumerable<SsasRoleMember> GetRoleExternalMembers(string roleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        var cacheKey = RoleExternalMembersCacheKey(roleName);
        var members = _cache.Get(cacheKey) as ICollection<SsasRoleMember>;

        if (members is null)
        {
            using var server = _parent.GetServer();
            var db = server.Databases.FindByName(_databaseName);
            var role = db.Model.Roles.Find(roleName) ?? throw new Exception($"Role: '{roleName}' does not exist.");

            members = role.Members
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

            _cache.Set(cacheKey, members, DateTime.Now.AddMinutes(CacheDurationMinutes));
        }

        return members;
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
            _cache.Remove(RoleExternalMembersCacheKey(roleName));

            return affected;
        }

        throw new Exception($"Role '{roleName}' does not exist.");
    }

    public void UpdateRole(string roleName, SsasDatabaseRole updated)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        if (_parent.IsProcessing(_databaseName))
        {
            throw new Exception($"'{_databaseName}' is currently being processed.");
        }

        using var server = _parent.GetServer();
        var db = server.Databases.FindByName(_databaseName);
        var role = db.Model.Roles.Find(roleName) ?? throw new Exception($"Role '{roleName}' does not exists."); ;

        role.Model.Description = updated.Description;
        role.ModelPermission = updated.Permission switch
        {
            SsasRolePermission.Administrator => TOM.ModelPermission.Administrator,
            SsasRolePermission.ReadRefresh => TOM.ModelPermission.ReadRefresh,
            _ => TOM.ModelPermission.Read
        };

        db.Model.SaveChanges();
        _cache.Remove(RolesCacheKey());
    }

    private string RoleExternalMembersCacheKey(string roleName) => $"{nameof(GetRoleExternalMembers)}:{_databaseName}:{roleName}";

    private string RolesCacheKey() => $"{nameof(Roles)}:{_databaseName}";
}