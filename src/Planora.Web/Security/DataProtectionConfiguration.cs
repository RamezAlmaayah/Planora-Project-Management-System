using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Planora.Web.Security;

public static class DataProtectionConfiguration
{
    public static void AddPlanoraDataProtection(this IServiceCollection services,
        IConfiguration configuration, bool requireSharedKeys)
    {
        if (requireSharedKeys || configuration.GetValue<bool>("DataProtection:PersistToSqlServer"))
        {
            var encoded = configuration["DataProtection:CertificateBase64"];
            var connection = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(encoded) || string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException("Shared Data Protection requires a database and protection certificate.");

            X509Certificate2 certificate;
            try
            {
                certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(encoded),
                    configuration["DataProtection:CertificatePassword"], X509KeyStorageFlags.EphemeralKeySet);
                if (!certificate.HasPrivateKey)
                    throw new InvalidOperationException();
            }
            catch
            {
                // Never include configuration values or certificate-loader exceptions.
                throw new InvalidOperationException("The Data Protection certificate is invalid or lacks its private key.");
            }

            services.AddDbContext<DataProtectionKeyDbContext>(options =>
                options.UseSqlServer(connection).EnableSensitiveDataLogging(false));
            services.AddDataProtection()
                .SetApplicationName("Planora")
                .PersistKeysToDbContext<DataProtectionKeyDbContext>()
                .ProtectKeysWithCertificate(certificate);
            return;
        }

        var path = configuration["DataProtection:KeyRingPath"];
        if (!string.IsNullOrWhiteSpace(path))
            services.AddDataProtection().SetApplicationName("Planora")
                .PersistKeysToFileSystem(new DirectoryInfo(path));
    }
}
