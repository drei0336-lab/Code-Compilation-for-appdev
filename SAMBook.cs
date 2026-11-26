using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Books > Update System Administration and Maintenance Book
    ///
    /// Writes the "Lesson Collection" book into System Administration and Maintenance (SAM, 1st Year, 2nd Semester): a Welcome page, Lessons 1-2 under Prelims, 3-4 Midterms, 5-6 Prefinals, 7-8 Finals, then Study Tips and a Closing page (same format as the other 1st Year books).
    /// It runs inside Unity, so the open Editor and the saved file always match. The book keeps its ID, so
    /// students' progress and bookmarks still work. Safe to run again: it only replaces this book's pages;
    /// other books and quizzes are kept.
    /// </summary>
    static class SAMBook
    {
        const string SubjectPath = "Assets/ARCHIV3D/Subjects/1st Year/SAM.asset";
        const string SubjectName = "SYSTEM ADMIN AND MAINTENANCE";
        const string BookTitle = "Lesson Collection";
        const string BookId = "b7d64e1c2f9a4c0e8a35d1f06c2e9b47";

        [MenuItem("Tools/ARCHIV3D/Books/Update System Administration and Maintenance Book", false, 47)]
        static void UpdateBook()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Stop Play mode first, then run this again.", "OK");
                return;
            }


            var subject = FindSubject();
            if (subject == null)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Couldn't find the " + SubjectName + " subject (" + SubjectPath + ").", "OK");
                return;
            }

            Undo.RecordObject(subject, "Update System Administration and Maintenance Book");
            if (subject.materials == null) subject.materials = new List<LearningMaterial>();

            var book = subject.materials.Find(m => m != null && m.title == BookTitle);
            if (book == null)
            {
                book = new LearningMaterial { materialId = BookId };
                subject.materials.Add(book);
            }
            if (string.IsNullOrEmpty(book.materialId)) book.materialId = BookId;

            book.title = BookTitle;
            book.subtitle = "Lessons 1 to 8";
            book.description = "For BSIT students. Eight short lessons on managing, securing, and maintaining computer systems and networks.";
            book.coverColor = subject.color;
            book.pages = Pages();

            subject.EnsureIds();
            EditorUtility.SetDirty(subject);
            AssetDatabase.SaveAssetIfDirty(subject);
            Selection.activeObject = subject;
            EditorGUIUtility.PingObject(subject);

            var headings = new List<string>();
            foreach (var p in book.pages) headings.Add(p.heading);
            EditorUtility.DisplayDialog("ARCHIV3D",
                subject.DisplayName + " > " + BookTitle + " now has " + book.pages.Count + " pages:\n\n" +
                string.Join(", ", headings) + "\n\nPress Play, then Tools > ARCHIV3D > Preview Bookshelf (Play mode) > " +
                "1st Year to read it.",
                "Nice");
        }

        static SubjectData FindSubject()
        {
            var subject = AssetDatabase.LoadAssetAtPath<SubjectData>(SubjectPath);
            if (subject != null) return subject;
            foreach (string guid in AssetDatabase.FindAssets("t:SubjectData"))
            {
                var s = AssetDatabase.LoadAssetAtPath<SubjectData>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null && s.subjectName == SubjectName) return s;
            }
            return null;
        }

        static BookPage Page(string heading, string text) => new BookPage { heading = heading, text = text, picture = null };

        static List<BookPage> Pages()
        {
            return new List<BookPage>
            {
                Page("<i><b>Welcome!</b></i>",
                     "<i>Welcome Reader! This is the 1st year shelf's book.\nThis semester was about keeping computer systems running: setting them up, securing them, and fixing them when they break. The lessons ahead follow the same order as the class: the system administrator's role, installing and configuring operating systems, users and permissions, the command line and automation, network services and remote access, security hardening, backups and maintenance, and monitoring and troubleshooting.</i>"),
                Page("Prelims",
                     "<b><i>LESSON 1: INTRODUCTION TO SYSTEM ADMINISTRATION</i></b>\n<i>Objectives: Describe the role of a system administrator and common IT environments.</i>\n\n\u2022 <indent=1.1em><b>System administrator (sysadmin):</b> responsible for installing, configuring, securing, monitoring, and maintaining computer systems, servers, and networks.</indent>\n\n\u2022 <indent=1.1em><b>Core responsibilities:</b> user and account management, software installation and updates, backups, security, performance monitoring, troubleshooting, and documentation.</indent>\n\n\u2022 <indent=1.1em><b>Environments:</b> on-premises servers, virtual machines, cloud platforms, and hybrid setups.</indent>\n\n\u2022 <indent=1.1em><b>CIA triad:</b> the goals of secure administration are Confidentiality, Integrity, and Availability.</indent>\n\n\u2022 <indent=1.1em><b>Good habits:</b> document changes, follow the principle of least privilege, test before deploying to production, and keep a change log.</indent>\n\n<b>Quick check:</b> <i>Which part of the CIA triad does a regular backup mainly support?</i>"),
                Page("Prelims",
                     "<b><i>LESSON 2: OPERATING SYSTEM INSTALLATION AND CONFIGURATION</i></b>\n<i>Objectives: Install and configure an operating system for use.</i>\n\n\u2022 <indent=1.1em><b>Common server and desktop OS:</b> Windows Server, Windows 10/11, Linux distributions (Ubuntu, Debian, CentOS-family).</indent>\n\n\u2022 <indent=1.1em><b>Installation steps:</b> verify hardware requirements, prepare bootable media, partition disks, install, apply updates, configure network settings, and create accounts.</indent>\n\n\u2022 <indent=1.1em><b>Virtualization:</b> run multiple operating systems on one machine using a hypervisor (VirtualBox, VMware, Hyper-V). Ideal for practice labs and snapshots.</indent>\n\n\u2022 <indent=1.1em><b>File systems:</b> NTFS (Windows), ext4 (Linux), FAT32/exFAT (removable drives).</indent>\n\n\u2022 <indent=1.1em><b>Post-install checklist:</b> set hostname, update system, install drivers, enable firewall, remove unused services.</indent>\n\n<b>Quick check:</b> <i>Why is a virtual machine useful for practicing installation?</i>"),
                Page("Midterms",
                     "<b><i>LESSON 3: USER, GROUP, AND PERMISSION MANAGEMENT</i></b>\n<i>Objectives: Control who can access what.</i>\n\n\u2022 <indent=1.1em><b>User accounts:</b> unique identity with credentials; groups simplify assigning permissions to many users at once.</indent>\n\n\u2022 <indent=1.1em><b>Authentication vs. authorization:</b> authentication proves who you are; authorization decides what you may do.</indent>\n\n\u2022 <indent=1.1em><b>Windows:</b> manage via Local Users and Groups or Active Directory (AD), which centralizes accounts, group policy, and domain management.</indent>\n\n\u2022 <indent=1.1em><b>Linux permissions:</b> read (r = 4), write (w = 2), execute (x = 1) for owner, group, and others. Example: <color=#8B4513>chmod 755 file</color> gives the owner full access and others read and execute.</indent>\n\n\u2022 <indent=1.1em><b>Best practices:</b> least privilege, strong password policies, MFA, disable or delete inactive accounts promptly.</indent>\n\n<indent=1.1em><color=#8B4513><noparse>useradd maria\nusermod -aG staff maria\nchmod 640 report.txt</noparse></color></indent>\n\n<b>Quick check:</b> <i>What does <color=#8B4513>chmod 644</color> allow for the owner, group, and others?</i>"),
                Page("Midterms",
                     "<b><i>LESSON 4: COMMAND LINE, SCRIPTING, AND TASK AUTOMATION</i></b>\n<i>Objectives: Perform administrative tasks efficiently using commands and scripts.</i>\n\n\u2022 <indent=1.1em><b>Why the command line:</b> faster, scriptable, works over remote connections, and uses fewer resources than a GUI.</indent>\n\n\u2022 <indent=1.1em><b>Common Linux commands:</b> <color=#8B4513>ls</color>, <color=#8B4513>cd</color>, <color=#8B4513>cp</color>, <color=#8B4513>mv</color>, <color=#8B4513>rm</color>, <color=#8B4513>cat</color>, <color=#8B4513>grep</color>, <color=#8B4513>ps</color>, <color=#8B4513>top</color>, <color=#8B4513>df</color>, <color=#8B4513>systemctl</color>.</indent>\n\n\u2022 <indent=1.1em><b>Common Windows commands:</b> <color=#8B4513>dir</color>, <color=#8B4513>ipconfig</color>, <color=#8B4513>tasklist</color>, <color=#8B4513>sfc /scannow</color>, and PowerShell cmdlets such as <color=#8B4513>Get-Process</color>.</indent>\n\n\u2022 <indent=1.1em><b>Scripting:</b> Bash (Linux) and PowerShell (Windows) automate repetitive work such as creating users or cleaning logs.</indent>\n\n\u2022 <indent=1.1em><b>Scheduling:</b> cron (Linux) and Task Scheduler (Windows) run jobs automatically.</indent>\n\n\u2022 <indent=1.1em><b>Safety:</b> test scripts in a lab first; be careful with destructive commands like <color=#8B4513>rm -rf</color>.</indent>\n\n<indent=1.1em><color=#8B4513><noparse>#!/bin/bash\n# Back up documents with today's date\ntar -czf /backup/docs-$(date +%F).tar.gz /home/shared/docs</noparse></color></indent>\n\n<b>Quick check:</b> <i>What tool would you use to run the script above every night?</i>"),
                Page("Prefinals",
                     "<b><i>LESSON 5: NETWORKING SERVICES AND REMOTE ADMINISTRATION</i></b>\n<i>Objectives: Configure essential network services and manage systems remotely.</i>\n\n\u2022 <indent=1.1em><b>IP configuration:</b> static vs. dynamic addressing; subnet mask, default gateway, DNS server.</indent>\n\n\u2022 <indent=1.1em><b>Key services:</b></indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>DHCP: automatically assigns IP addresses</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>DNS: translates names to IP addresses</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>File and print services: shared resources on the network</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Web, mail, and database servers: host applications and data</indent>\n\n\u2022 <indent=1.1em><b>Remote access:</b> SSH (secure shell for Linux), RDP (Windows Remote Desktop). Avoid unencrypted tools such as Telnet.</indent>\n\n\u2022 <indent=1.1em><b>Troubleshooting commands:</b> <color=#8B4513>ping</color> (reachability), <color=#8B4513>traceroute</color> / <color=#8B4513>tracert</color> (path), <color=#8B4513>nslookup</color> / <color=#8B4513>dig</color> (DNS), <color=#8B4513>ipconfig</color> / <color=#8B4513>ip a</color> (addresses), <color=#8B4513>netstat</color> / <color=#8B4513>ss</color> (connections).</indent>\n\n<b>Quick check:</b> <i>A user can reach a site by IP address but not by name. Which service is likely failing?</i>"),
                Page("Prefinals",
                     "<b><i>LESSON 6: SECURITY HARDENING AND UPDATES</i></b>\n<i>Objectives: Reduce a system's attack surface and keep it current.</i>\n\n\u2022 <indent=1.1em><b>Hardening:</b> disable unused services and ports, remove default accounts, enforce strong passwords, and apply the principle of least privilege.</indent>\n\n\u2022 <indent=1.1em><b>Firewalls:</b> filter traffic by rules (e.g., Windows Defender Firewall, <color=#8B4513>ufw</color> / <color=#8B4513>iptables</color>).</indent>\n\n\u2022 <indent=1.1em><b>Patch management:</b> regularly apply operating system and application updates; test critical patches before wide rollout when possible.</indent>\n\n\u2022 <indent=1.1em><b>Endpoint protection:</b> antivirus or anti-malware, plus user awareness training against phishing.</indent>\n\n\u2022 <indent=1.1em><b>Encryption:</b> protect data at rest (disk encryption such as BitLocker) and in transit (HTTPS, VPN, SSH).</indent>\n\n\u2022 <indent=1.1em><b>Logging and auditing:</b> review logs for failed logins and unusual activity.</indent>\n\n<b>Quick check:</b> <i>Why is leaving unused services running a security risk?</i>"),
                Page("Finals",
                     "<b><i>LESSON 7: BACKUP, RECOVERY, AND PREVENTIVE MAINTENANCE</i></b>\n<i>Objectives: Protect data and keep hardware and software healthy.</i>\n\n\u2022 <indent=1.1em><b>Backup types:</b> full (everything), incremental (changes since the last backup of any type), differential (changes since the last full backup).</indent>\n\n\u2022 <indent=1.1em><b>3-2-1 rule:</b> keep 3 copies of data, on 2 different media types, with 1 copy offsite. Test restores regularly; an untested backup is not a backup.</indent>\n\n\u2022 <indent=1.1em><b>Disaster recovery:</b> define RPO (how much data loss is acceptable) and RTO (how fast service must return).</indent>\n\n\u2022 <indent=1.1em><b>RAID overview:</b> RAID 0 (speed, no redundancy), RAID 1 (mirroring), RAID 5 (striping with parity). RAID improves availability but is not a backup.</indent>\n\n\u2022 <indent=1.1em><b>Preventive hardware maintenance:</b> clean dust from fans and vents, check cables and connections, monitor temperature, keep firmware and drivers updated, and use a UPS for power protection.</indent>\n\n\u2022 <indent=1.1em><b>Software maintenance:</b> disk cleanup, defragmenting HDDs (not needed for SSDs), removing unused software, and reviewing startup programs.</indent>\n\n<b>Quick check:</b> <i>Which backup type needs the least storage and time per run?</i>"),
                Page("Finals",
                     "<b><i>LESSON 8: MONITORING, TROUBLESHOOTING, AND DOCUMENTATION</i></b>\n<i>Objectives: Detect problems early and resolve them systematically.</i>\n\n\u2022 <indent=1.1em><b>Monitoring:</b> track CPU, memory, disk space, network usage, and service uptime. Tools include Task Manager, <color=#8B4513>top</color> / <color=#8B4513>htop</color>, Resource Monitor, Nagios, Zabbix, and cloud dashboards.</indent>\n\n\u2022 <indent=1.1em><b>Logs:</b> Windows Event Viewer and Linux <color=#8B4513>/var/log</color> files reveal errors and security events.</indent>\n\n\u2022 <indent=1.1em><b>Troubleshooting method:</b></indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>1. Identify the problem and gather information</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>2. Establish a theory of probable cause</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>3. Test the theory</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>4. Plan and implement a fix</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>5. Verify full system functionality</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>6. Document findings and actions</indent>\n\n\u2022 <indent=1.1em><b>Help desk and ticketing:</b> record, prioritize, and track user issues; follow escalation procedures.</indent>\n\n\u2022 <indent=1.1em><b>Documentation:</b> network diagrams, configuration records, procedures, and asset inventories save time and prevent repeated mistakes.</indent>\n\n<b>Quick check:</b> <i>Why is the last step of the troubleshooting method (documenting) important?</i>"),
                Page("Study Tips",
                     "<b><i>SUGGESTED STUDY TIPS</i></b>\n\n\u2022 <indent=1.1em>Build a home lab with virtual machines; break things safely and fix them.</indent>\n\n\u2022 <indent=1.1em>Practice commands daily until they are second nature.</indent>\n\n\u2022 <indent=1.1em>Keep a personal troubleshooting journal of problems and solutions.</indent>\n\n\u2022 <indent=1.1em>Always ask before changing anything: what could go wrong, and how do I undo it?</indent>"),
                Page("Closing",
                     "That is the work that keeps every system running: setting machines up, controlling who can use them, automating the routine, connecting and securing them, protecting their data, and fixing problems methodically. Every server, lab or network you look after from here builds on these eight lessons.")
            };
        }
    }
}
