using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace KubSMTP.Services;

internal static class TlsCertificateDiagnostics
{
 public static IEnumerable<string> Describe(string host,X509Certificate? certificate,X509Chain? chain,SslPolicyErrors errors,bool german)
 {
  string T(string de,string en)=>german?de:en;
  // Certificate names and platform messages come from outside the application.
  static string OneLine(string value)=>string.Join(" ",value.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));

  if(errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
   yield return T("Der Server hat kein Zertifikat bereitgestellt.","The server did not provide a certificate.");
  if(errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
   yield return T($"Hostname stimmt nicht überein: Das Zertifikat gilt nicht für '{OneLine(host)}'.",$"Hostname mismatch: The certificate is not valid for '{OneLine(host)}'.");

  var certificates=new List<X509Certificate2>();
  using var leaf=certificate is null?null:new X509Certificate2(certificate);
  if(leaf is not null)certificates.Add(leaf);
  if(chain is not null)certificates.AddRange(chain.ChainElements.Cast<X509ChainElement>().Select(element=>element.Certificate));
  var now=DateTime.UtcNow;
  foreach(var item in certificates.DistinctBy(item=>item.Thumbprint))
  {
   var name=OneLine(item.Subject);
   if(now>item.NotAfter.ToUniversalTime())
    yield return T($"Zertifikat '{name}' ist abgelaufen (gültig bis {item.NotAfter.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC).",$"Certificate '{name}' has expired (valid until {item.NotAfter.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC).");
   if(now<item.NotBefore.ToUniversalTime())
    yield return T($"Zertifikat '{name}' ist noch nicht gültig (gültig ab {item.NotBefore.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC).",$"Certificate '{name}' is not yet valid (valid from {item.NotBefore.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC).");
  }

  if(!errors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))yield break;
  if(chain is null||chain.ChainStatus.Length==0)
  {
   yield return T("Zertifikatskette konnte nicht überprüft werden; keine weiteren Details verfügbar.","Certificate chain could not be validated; no further details available.");
   yield break;
  }
  foreach(var status in chain.ChainStatus)
  {
   foreach(var flag in Enum.GetValues<X509ChainStatusFlags>())
   {
    if(flag==X509ChainStatusFlags.NoError||!status.Status.HasFlag(flag))continue;
    var reason=flag switch
    {
     X509ChainStatusFlags.NotTimeValid=>T("Gültigkeitszeitraum eines Zertifikats in der Kette ist ungültig.","The validity period of a certificate in the chain is invalid."),
     X509ChainStatusFlags.NotTimeNested=>T("Gültigkeitszeiträume der Zertifikatskette passen nicht zusammen.","Certificate chain validity periods are not nested correctly."),
     X509ChainStatusFlags.UntrustedRoot=>T("Stammzertifikat ist nicht vertrauenswürdig (z. B. selbstsigniert oder unbekannte Zertifizierungsstelle).","The root certificate is not trusted (e.g. self-signed or unknown certificate authority)."),
     X509ChainStatusFlags.PartialChain=>T("Zertifikatskette ist unvollständig; ein Ausstellerzertifikat fehlt.","The certificate chain is incomplete; an issuer certificate is missing."),
     X509ChainStatusFlags.Revoked=>T("Zertifikat wurde widerrufen.","The certificate has been revoked."),
     X509ChainStatusFlags.RevocationStatusUnknown=>T("Widerrufsstatus des Zertifikats konnte nicht ermittelt werden.","The certificate revocation status could not be determined."),
     X509ChainStatusFlags.OfflineRevocation=>T("Widerrufsprüfung ist offline bzw. nicht erreichbar.","The revocation service is offline or unreachable."),
     X509ChainStatusFlags.NotSignatureValid=>T("Zertifikatsignatur ist ungültig.","The certificate signature is invalid."),
     X509ChainStatusFlags.NotValidForUsage=>T("Zertifikat ist für diesen Verwendungszweck nicht gültig.","The certificate is not valid for this usage."),
     X509ChainStatusFlags.ExplicitDistrust=>T("Zertifikat ist ausdrücklich als nicht vertrauenswürdig eingestuft.","The certificate is explicitly distrusted."),
     X509ChainStatusFlags.HasWeakSignature=>T("Zertifikat verwendet eine schwache Signatur.","The certificate uses a weak signature."),
     _=>T("Fehler in der Zertifikatskette.","Certificate chain validation error.")
    };
    var details=OneLine(status.StatusInformation);
    yield return $"{reason} [{flag}]"+(details.Length==0?"":" "+details);
   }
  }
 }
}
