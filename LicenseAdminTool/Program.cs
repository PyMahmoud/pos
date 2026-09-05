using System;
using System.IO;
using Core.Licensing.Signing;

namespace LicenseAdminTool
{
    /// <summary>
    /// Admin-only console tool for issuing signed RovaShop POS licenses.
    /// NEVER shipped to a client — run only on Baraa/Mahmoud's own
    /// machine. See Licensing-Plan.md, Phase 4.
    ///
    /// Needs the PRIVATE key XML (PrivateSigningKey.xml at repo root) to
    /// sign anything. Looks for it automatically by walking up from the
    /// exe's folder; falls back to asking for a path if not found.
    /// </summary>
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("=== RovaShop POS — License Admin Tool ===");
            Console.WriteLine();

            string privateKeyXml = LoadPrivateKey();
            if (privateKeyXml == null)
            {
                Console.WriteLine("Could not load a private key. Aborting.");
                Pause();
                return;
            }

            string hardAnchorHash = PromptForHardAnchorHash();
            int durationDays = PromptForDurationDays();
            string note = PromptForNote();

            var data = new LicenseData
            {
                HardAnchorHash = hardAnchorHash,
                IssuedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow.AddDays(durationDays),
                Note = note
            };

            string blob = LicenseSigner.Sign(data, privateKeyXml);

            Console.WriteLine();
            Console.WriteLine("=== License generated ===");
            Console.WriteLine("Issued (UTC):  " + data.IssuedUtc);
            Console.WriteLine("Expires (UTC): " + data.ExpiresUtc + "  (" + durationDays + " days)");
            Console.WriteLine("Note:          " + (string.IsNullOrEmpty(note) ? "(none)" : note));
            Console.WriteLine();
            Console.WriteLine("License blob (send this to the client / paste into the activation screen):");
            Console.WriteLine();
            Console.WriteLine(blob);
            Console.WriteLine();

            string outputPath = SaveToFile(blob, note);
            if (outputPath != null)
            {
                Console.WriteLine("Also saved to: " + outputPath);
            }

            Pause();
        }

        private static string LoadPrivateKey()
        {
            string found = FindPrivateKeyFile();
            if (found != null)
            {
                Console.WriteLine("Using private key: " + found);
                return File.ReadAllText(found);
            }

            Console.WriteLine("Couldn't auto-locate PrivateSigningKey.xml near the repo root.");
            Console.Write("Enter the full path to the private key XML file: ");
            string path = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Console.WriteLine("File not found: " + path);
                return null;
            }

            return File.ReadAllText(path);
        }

        /// <summary>
        /// Walks up from the executable's folder (typically deep inside
        /// bin\Debug\...) looking for PrivateSigningKey.xml, which lives
        /// at the repo root. Stops after a reasonable number of levels so
        /// it can't wander off outside the repo entirely.
        /// </summary>
        private static string FindPrivateKeyFile()
        {
            const string fileName = "PrivateSigningKey.xml";
            const int maxLevelsUp = 8;

            DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            for (int i = 0; i < maxLevelsUp && dir != null; i++)
            {
                string candidate = Path.Combine(dir.FullName, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }

            return null;
        }

        private static string PromptForHardAnchorHash()
        {
            Console.WriteLine();
            Console.WriteLine("Paste the client's Hard Anchor Hash (from their activation screen).");
            Console.Write("Hard Anchor Hash: ");
            string hash = Console.ReadLine();
            return (hash ?? string.Empty).Trim();
        }

        private static int PromptForDurationDays()
        {
            Console.WriteLine();
            Console.WriteLine("License duration:");
            Console.WriteLine("  1) 6 months  (182 days)");
            Console.WriteLine("  2) 1 year    (365 days)");
            Console.WriteLine("  3) 2 years   (730 days)");
            Console.WriteLine("  4) Custom (enter number of days)");
            Console.Write("Choice [1-4]: ");

            string choice = (Console.ReadLine() ?? string.Empty).Trim();

            switch (choice)
            {
                case "1":
                    return 182;
                case "2":
                    return 365;
                case "3":
                    return 730;
                case "4":
                    return PromptForCustomDays();
                default:
                    Console.WriteLine("Unrecognized choice, defaulting to 1 year.");
                    return 365;
            }
        }

        private static int PromptForCustomDays()
        {
            Console.Write("Number of days: ");
            string input = Console.ReadLine();
            int days;
            if (int.TryParse(input, out days) && days > 0)
            {
                return days;
            }

            Console.WriteLine("Invalid input, defaulting to 365 days.");
            return 365;
        }

        private static string PromptForNote()
        {
            Console.WriteLine();
            Console.Write("Note for your own records (e.g. client name) [optional]: ");
            return Console.ReadLine();
        }

        private static string SaveToFile(string blob, string note)
        {
            try
            {
                string safeNote = string.IsNullOrWhiteSpace(note)
                    ? "license"
                    : SanitizeForFileName(note);

                string fileName = safeNote + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".lic";
                string path = Path.Combine(Environment.CurrentDirectory, fileName);

                File.WriteAllText(path, blob);
                return path;
            }
            catch (IOException)
            {
                // Not saving to a file isn't fatal — the blob is already
                // printed to the console above and can be copied by hand.
                return null;
            }
        }

        private static string SanitizeForFileName(string input)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var chars = input.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                foreach (char c in invalid)
                {
                    if (chars[i] == c)
                    {
                        chars[i] = '_';
                        break;
                    }
                }
            }
            return new string(chars);
        }

        private static void Pause()
        {
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
