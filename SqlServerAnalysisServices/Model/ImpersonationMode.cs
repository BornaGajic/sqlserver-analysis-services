namespace SqlServerAnalysisServices.Model;

public enum ImpersonationMode
{
    //
    // Summary:
    //     Uses the inherited value from the ImpersonationMode on the DataSourceImpersonationInfo
    //     object in the database.
    Default,

    //
    // Summary:
    //     The credentials of the service account are used.
    ImpersonateServiceAccount,

    //
    // Summary:
    //     Currently not supported.
    ImpersonateAnonymous,

    //
    // Summary:
    //     The current user is impersonated.
    ImpersonateCurrentUser,

    //
    // Summary:
    //     This option is used when the service uses the account and (optionally) a password
    //     associated with the data source.
    ImpersonateAccount,

    //
    // Summary:
    //     Do not reference this member directly in your code. It supports the Analysis
    //     Services infrastructure.
    ImpersonateUnattendedAccount
}