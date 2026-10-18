using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using BibleApp.Data;
using navigationshurely.models;
using navigationshurely.Services;

namespace navigationshurely.services
{
    public class Linguist
    {
        private State state;
        private ParseState parseState;
        private Command? command;
        private string commandBuffer = string.Empty;
        private string choiceCommandBuffer = string.Empty;
        private int quantifier = 0;
        private EntireCommandStatus validity;
        private State previousStateBeforeSearch;
        private char? activeFilter;
        private string searchQuery = string.Empty;
        private string wholeQueryWithCommas;
        private string commandString = string.Empty;
        private bool normalChoosingMode = true;

        public event Action<bool>? SearchUiVisibilityChanged;

        public Linguist()
        {
            state = State.B;
            parseState = ParseState.Start;
            command = null;
            validity = EntireCommandStatus.Incomplete;
            wholeQueryWithCommas = ",,,,,lt,os,mf";
        }

        public string CurrentCommandInfo()
        {
            String result = commandString;
            if (validity == EntireCommandStatus.Incomplete) 
            {
                return result;
            }
            commandString = "";
            return result;
        }

        private void AddCharToCommandInfo(char k)
        {
            switch (k)
            {
                case '{':
                    break;
                case '+':
                    commandString += "[Tab]";
                    break;
                case '|':
                    commandString += "Esc.";
                    break;
                case '<':
                    commandString += "Ctrl+D";
                    break;
                case '>':
                    commandString += "Ctrl+U";
                    break;
                default:
                    commandString += k;
                    break;
            }
        }

        public Color CommandColor()
        {
            switch (validity)
            {
                case EntireCommandStatus.Incomplete:
                    return Color.FromRgb(85, 200, 208);
                case EntireCommandStatus.Invalid:
                    return Color.FromRgb(255, 107, 120);
                case EntireCommandStatus.Valid:
                    return Color.FromRgb(142, 214, 165);
                default:
                    return Color.FromRgb(197, 206, 216);
            }
        }


        private static readonly Dictionary<char, int> Quantifiers = new()
        {
            ['z'] = 0,
            ['u'] = 1,
            ['d'] = 2,
            ['t'] = 3,
            ['q'] = 4,
            ['a'] = 4,
            ['i'] = 5,
            ['s'] = 6,
            ['e'] = 7,
            ['o'] = 8,
            ['n'] = 9
        };


        private static readonly Dictionary<string, int> BCommands = new()
        {
            ["ge"] = 1, //genesis
            ["x"] = 2, //exodo
            ["le"] = 3, //levitico
            ["bi"] = 4, //bilang
            ["de"] = 5, //deuteronomio
            ["su"] = 6, //josue
            ["uk"] = 7, //hukom
            ["ru"] = 8, //ruth
            ["is"] = 9, //I samuel
            ["ws"] = 10, // II samuel
            ["ih"] = 11, //I hari
            ["wh"] = 12, //II hari
            ["icr"] = 13, //I cronica
            ["wcr"] = 14, //II cronica
            ["ez"] = 15, //ezra
            ["ne"] = 16, //nehemias
            ["es"] = 17, //esther
            ["ob"] = 18, //job
            ["aw"] = 19, //awit
            ["wi"] = 20, //kawikaan
            ["ec"] = 21, //eclesiastes
            ["so"] = 22, //awit ng mga awit
            ["sa"] = 23, //isaias
            ["re"] = 24, //jeremias
            ["y"] = 25, //panaghoy
            ["ek"] = 26, //ezekiel
            ["da"] = 27, //daniel
            ["os"] = 28, //oseas
            ["oe"] = 29, //joel
            ["am"] = 30, //amos
            ["ba"] = 31, //obadias
            ["on"] = 32, //jonas
            ["mi"] = 33, //mikas
            ["na"] = 34, //nahum
            ["ha"] = 35, //habacuc
            ["ze"] = 36, //zefanias
            ["ag"] = 37, //hagai
            ["za"] = 38, //zacarias
            ["mal"] = 39, //malakias
            ["mat"] = 40, //mateo
            ["mar"] = 41, //marcos
            ["lu"] = 42, //lucas
            ["ua"] = 43, //juan
            ["gw"] = 44, //gawa
            ["ro"] = 45, //roma
            ["ico"] = 46, //I corinto
            ["wco"] = 47, //II corinto
            ["ga"] = 48, //galacia
            ["ef"] = 49, //efeso
            ["fp"] = 50, //filipos
            ["ls"] = 51, //colosas
            ["its"] = 52, //I tesalonica
            ["wts"] = 53, //II tesalonica
            ["itm"] = 54, //I timoteo
            ["wtm"] = 55, //II timoteo
            ["ti"] = 56, //tito
            ["fm"] = 57, //filemon
            ["he"] = 58, //hebreo
            ["sn"] = 59, //santiago
            ["p"] = 60, //I pedro
            ["wp"] = 61, //II pedro
            ["iua"] = 62, //I juan
            ["wua"] = 63, //II juan
            ["tua"] = 64, //III juan
            ["ud"] = 65, //judas
            ["ap"] = 66 //apocalipsis
        };


        private static readonly HashSet<string> BPrefixes = BuildPrefixes();

        private static HashSet<string> BuildPrefixes()
        {
            var prefixes = new HashSet<string>();

            foreach (var command in BCommands.Keys)
            {
                for (int i = 1; i < command.Length; i++)
                {
                    prefixes.Add(command[..i]);
                }
            }

            return prefixes;
        }


        // Aliases derived from Ang Dating Biblia (Tagalog).
        // Each key is the first letter of the verse's first four space-separated words.
        // Values encode BookId * 1,000,000 + Chapter * 1,000 + Verse.
        // Rows marked NEW are the 250 added aliases.
        private static readonly Dictionary<string, int> SCommands = new()
        {
            ["npan"] = 1001001, // Genesis 1:1
            ["aala"] = 1001002, // Genesis 1:2
            ["asnd"] = 1001003, // Genesis 1:3
            ["atnd"] = 1001005, // Genesis 1:5
            ["agnd"] = 1001007, // Genesis 1:7
            ["annd"] = 1001027, // Genesis 1:27
            ["anal"] = 1002001, // Genesis 2:1
            ["ania"] = 1002002, // Genesis 2:2
            ["abnd"] = 1002003, // Genesis 2:3
            ["iapn"] = 1002004, // Genesis 2:4
            ["annp"] = 1002007, // Genesis 2:7
            ["aana"] = 1003001, // Genesis 3:1
            ["asnb"] = 1003002, // Genesis 3:2
            ["dsbn"] = 1003003, // Genesis 3:3
            ["asna"] = 1003004, // Genesis 3:4
            ["anmn"] = 1003006, // Genesis 3:6
            ["snnp"] = 1012001, // Genesis 12:1
            ["agki"] = 1012002, // Genesis 12:2
            ["apka"] = 1012003, // Genesis 12:3
            ["aink"] = 1050016, // Genesis 50:16
            ["gsnk"] = 1050017, // Genesis 50:17
            ["anak"] = 1050018, // Genesis 50:18
            ["atsi"] = 1050020, // Genesis 50:20
            ["knhk"] = 1050021, // Genesis 50:21
            ["aapa"] = 2014001, // Exodus 14:1
            ["smsm"] = 2014002, // Exodus 14:2
            ["asnf"] = 2014003, // Exodus 14:3
            ["ansh"] = 2014005, // Exodus 14:5
            ["ainf"] = 2014006, // Exodus 14:6
            ["iknp"] = 2014014, // Exodus 14:14
            ["aapm"] = 2020002, // Exodus 20:2
            ["hkmn"] = 2020003, // Exodus 20:3
            ["hkgp"] = 2020004, // Exodus 20:4
            ["hmys"] = 2020005, // Exodus 20:5
            ["apkn"] = 2020006, // Exodus 20:6
            ["hmba"] = 2020007, // Exodus 20:7
            ["imai"] = 2020012, // Exodus 20:12
            ["hkmo"] = 3019018, // Leviticus 19:18
            ["pknn"] = 4006024, // Numbers 6:24
            ["pnnp"] = 4006025, // Numbers 6:25
            ["innp"] = 4006026, // Numbers 6:26
            ["inau"] = 5006001, // Deuteronomy 6:1
            ["niam"] = 5006002, // Deuteronomy 6:2
            ["dmno"] = 5006003, // Deuteronomy 6:3
            ["dmoi"] = 5006004, // Deuteronomy 6:4
            ["aiia"] = 5006005, // Deuteronomy 6:5
            ["aams"] = 5006006, // Deuteronomy 6:6
            ["kmam"] = 5031006, // Deuteronomy 31:6
            ["nnpn"] = 6001001, // Joshua 1:1
            ["smna"] = 6001002, // Joshua 1:2
            ["msia"] = 6001004, // Joshua 1:4
            ["wmst"] = 6001005, // Joshua 1:5
            ["mkam"] = 6001006, // Joshua 1:6
            ["aani"] = 6001008, // Joshua 1:8
            ["hbki"] = 6001009, // Joshua 1:9
            ["akka"] = 6024003, // Joshua 24:3
            ["aikk"] = 6024004, // Joshua 24:4
            ["aass"] = 6024005, // Joshua 24:5
            ["aika"] = 6024006, // Joshua 24:6
            ["ansd"] = 6024007, // Joshua 24:7
            ["akin"] = 6024015, // Joshua 24:15
            ["asnr"] = 8001016, // Ruth 1:16
            ["nsnp"] = 9016007, // 1 Samuel 16:7
            ["kaab"] = 14007014, // 2 Chronicles 7:14
            ["asnh"] = 18001021, // Job 1:21
            ["nknm"] = 18042002, // Job 42:2
            ["matn"] = 19001001, // Psalms 1:1
            ["apaa"] = 19023001, // Psalms 23:1
            ["kpas"] = 19023002, // Psalms 23:2
            ["kpaa"] = 19023003, // Psalms 23:3
            ["obal"] = 19023004, // Psalms 23:4
            ["ipan"] = 19023005, // Psalms 23:5
            ["tnak"] = 19023006, // Psalms 23:6
            ["ndsa"] = 19027002, // Psalms 27:2
            ["ibah"] = 19027004, // Psalms 27:4
            ["dmop"] = 19027007, // Psalms 27:7
            ["nish"] = 19027008, // Psalms 27:8
            ["apap"] = 19034001, // Psalms 34:1
            ["aaka"] = 19034002, // Psalms 34:2
            ["odnn"] = 19034003, // Psalms 34:3
            ["ahap"] = 19034004, // Psalms 34:4
            ["oita"] = 19034008, // Psalms 34:8
            ["apam"] = 19034018, // Psalms 34:18
            ["hkmd"] = 19037001, // Psalms 37:1
            ["ssmp"] = 19037002, // Psalms 37:2
            ["mkns"] = 19037004, // Psalms 37:4
            ["akpg"] = 19037006, // Psalms 37:6
            ["mknp"] = 19037008, // Psalms 37:8
            ["samm"] = 19037009, // Psalms 37:9
            ["alnt"] = 19037023, // Psalms 37:23
            ["adaa"] = 19046001, // Psalms 46:1
            ["khtm"] = 19046002, // Psalms 46:2
            ["miin"] = 19046004, // Psalms 46:4
            ["adan"] = 19046005, // Psalms 46:5
            ["amba"] = 19046006, // Psalms 46:6
            ["apnm"] = 19046007, // Psalms 46:7
            ["kama"] = 19046010, // Psalms 46:10
            ["mksa"] = 19051001, // Psalms 51:1
            ["hmal"] = 19051002, // Psalms 51:2
            ["skka"] = 19051003, // Psalms 51:3
            ["lsis"] = 19051004, // Psalms 51:4
            ["nais"] = 19051005, // Psalms 51:5
            ["lman"] = 19051010, // Psalms 51:10
            ["spam"] = 19056003, // Psalms 56:3
            ["stsl"] = 19091001, // Psalms 91:1
            ["asts"] = 19091002, // Psalms 91:2
            ["skik"] = 19091003, // Psalms 91:3
            ["ktkn"] = 19091004, // Psalms 91:4
            ["iahm"] = 19091005, // Psalms 91:5
            ["dssn"] = 19091006, // Psalms 91:6
            ["ilam"] = 19091007, // Psalms 91:7
            ["imln"] = 19091008, // Psalms 91:8
            ["siop"] = 19091009, // Psalms 91:9
            ["wkms"] = 19091010, // Psalms 91:10
            ["kdks"] = 19091012, // Psalms 91:12
            ["iyal"] = 19091013, // Psalms 91:13
            ["stsa"] = 19091015, // Psalms 91:15
            ["absn"] = 19091016, // Psalms 91:16
            ["anna"] = 19100003, // Psalms 100:3
            ["sapa"] = 19100005, // Psalms 100:5
            ["pmap"] = 19103001, // Psalms 103:1
            ["nsnn"] = 19103003, // Psalms 103:3
            ["nstn"] = 19103004, // Psalms 103:4
            ["nsbs"] = 19103005, // Psalms 103:5
            ["kiak"] = 19103007, // Psalms 103:7
            ["shgs"] = 19103010, // Psalms 103:10
            ["kgal"] = 19103012, // Psalms 103:12
            ["iaan"] = 19118024, // Psalms 118:24
            ["msss"] = 19119001, // Psalms 119:1
            ["msnn"] = 19119002, // Psalms 119:2
            ["oshn"] = 19119003, // Psalms 119:3
            ["iisa"] = 19119004, // Psalms 119:4
            ["omna"] = 19119005, // Psalms 119:5
            ["spln"] = 19119009, // Psalms 119:9
            ["asma"] = 19119011, // Psalms 119:11
            ["asmi"] = 19119105, // Psalms 119:105
            ["ikaa"] = 19121001, // Psalms 121:1
            ["assa"] = 19121002, // Psalms 121:2
            ["apat"] = 19121005, // Psalms 121:5
            ["hksn"] = 19121006, // Psalms 121:6
            ["minp"] = 19127001, // Psalms 127:1
            ["mnnp"] = 19133001, // Psalms 133:1
            ["opis"] = 19139001, // Psalms 139:1
            ["inaa"] = 19139002, // Psalms 139:2
            ["isaa"] = 19139003, // Psalms 139:3
            ["swpa"] = 19139004, // Psalms 139:4
            ["ikas"] = 19139005, // Psalms 139:5
            ["amsi"] = 19139014, // Psalms 139:14
            ["pnbb"] = 19150006, // Psalms 150:6
            ["sata"] = 20001024, // Proverbs 1:24
            ["akhm"] = 20003001, // Proverbs 3:1
            ["sknm"] = 20003002, // Proverbs 3:2
            ["hkpn"] = 20003003, // Proverbs 3:3
            ["tksp"] = 20003005, // Proverbs 3:5
            ["kmss"] = 20003006, // Proverbs 3:6
            ["mksi"] = 20003008, // Proverbs 3:8
            ["apsp"] = 20009010, // Proverbs 9:10
            ["apah"] = 20010012, // Proverbs 10:12
            ["ahnm"] = 20015008, // Proverbs 15:8
            ["ampn"] = 20016001, // Proverbs 16:1
            ["imsp"] = 20016003, // Proverbs 16:3
            ["gnpa"] = 20016004, // Proverbs 16:4
            ["spnk"] = 20016006, // Proverbs 16:6
            ["paml"] = 20016007, // Proverbs 16:7
            ["apnt"] = 20016009, // Proverbs 16:9
            ["agnt"] = 20016011, // Proverbs 16:11
            ["akan"] = 20016018, // Proverbs 16:18
            ["mmsa"] = 20016024, // Proverbs 16:24
            ["akau"] = 20017017, // Proverbs 17:17
            ["ahah"] = 20018001, // Proverbs 18:1
            ["amaw"] = 20018002, // Proverbs 18:2
            ["pama"] = 20018003, // Proverbs 18:3
            ["amsn"] = 20018004, // Proverbs 18:4
            ["amln"] = 20018006, // Proverbs 18:6
            ["apnp"] = 20018010, // Proverbs 18:10
            ["kaba"] = 20018021, // Proverbs 18:21
            ["mmks"] = 20019021, // Proverbs 19:21
            ["aksk"] = 20022004, // Proverbs 22:4
            ["tmab"] = 20022006, // Proverbs 22:6
            ["aban"] = 20027017, // Proverbs 27:17
            ["imnb"] = 20031010, // Proverbs 31:10
            ["sbba"] = 21003001, // Ecclesiastes 3:1
            ["iawn"] = 21012013, // Ecclesiastes 12:13
            ["anka"] = 23006008, // Isaiah 6:8
            ["ssaa"] = 23009006, // Isaiah 9:6
            ["iiss"] = 23026003, // Isaiah 26:3
            ["amnk"] = 23033006, // Isaiah 33:6
            ["iaia"] = 23040001, // Isaiah 40:1
            ["mkmp"] = 23040002, // Isaiah 40:2
            ["atni"] = 23040003, // Isaiah 40:3
            ["oinn"] = 23040009, // Isaiah 40:9
            ["napd"] = 23040010, // Isaiah 40:10
            ["nsns"] = 23040031, // Isaiah 40:31
            ["mksh"] = 23041001, // Isaiah 41:1
            ["snni"] = 23041002, // Isaiah 41:2
            ["khsa"] = 23041003, // Isaiah 41:3
            ["sgay"] = 23041004, // Isaiah 41:4
            ["nnmp"] = 23041005, // Isaiah 41:5
            ["hkms"] = 23041010, // Isaiah 41:10
            ["nnga"] = 23043001, // Isaiah 43:1
            ["piad"] = 23043002, // Isaiah 43:2
            ["assh"] = 23043006, // Isaiah 43:6
            ["iiab"] = 23043008, // Isaiah 43:8
            ["snsa"] = 23053001, // Isaiah 53:1
            ["shai"] = 23053003, // Isaiah 53:3
            ["nsnd"] = 23053005, // Isaiah 53:5
            ["tlng"] = 23053006, // Isaiah 53:6
            ["sngm"] = 23053007, // Isaiah 53:7
            ["agna"] = 23053009, // Isaiah 53:9
            ["wang"] = 23054017, // Isaiah 54:17
            ["saam"] = 23055008, // Isaiah 55:8
            ["inam"] = 24029001, // Jeremiah 29:1
            ["pnms"] = 24029002, // Jeremiah 29:2
            ["aiha"] = 24029007, // Jeremiah 29:7
            ["sgas"] = 24029008, // Jeremiah 29:8
            ["snka"] = 24029011, // Jeremiah 29:11
            ["akms"] = 24029012, // Jeremiah 29:12
            ["amsk"] = 24030019, // Jeremiah 30:19
            ["spys"] = 24031001, // Jeremiah 31:1
            ["apan"] = 24031003, // Jeremiah 31:3
            ["mika"] = 24031004, // Jeremiah 31:4
            ["mkmn"] = 24031005, // Jeremiah 31:5
            ["nads"] = 24031008, // Jeremiah 31:8
            ["tksa"] = 24033003, // Jeremiah 33:3
            ["smkn"] = 25003022, // Lamentations 3:22
            ["amyb"] = 25003023, // Lamentations 3:23
            ["naad"] = 27003017, // Daniel 3:17
            ["kisi"] = 33006008, // Micah 6:8
            ["apmk"] = 34001007, // Nahum 1:7
            ["nakk"] = 35002004, // Habakkuk 2:4
            ["sbap"] = 35003017, // Habakkuk 3:17
            ["apmd"] = 36003017, // Zephaniah 3:17
            ["dnab"] = 39003010, // Malachi 3:10
            ["mamm"] = 40005003, // Matthew 5:3
            ["mans"] = 40005004, // Matthew 5:4
            ["mana"] = 40005006, // Matthew 5:6
            ["kain"] = 40005014, // Matthew 5:14
            ["lnga"] = 40005016, // Matthew 5:16
            ["dsks"] = 40005044, // Matthew 5:44
            ["dipi"] = 40006006, // Matthew 6:6
            ["mnkn"] = 40006009, // Matthew 6:9
            ["kmkn"] = 40006020, // Matthew 6:20
            ["sksn"] = 40006021, // Matthew 6:21
            ["mnam"] = 40006026, // Matthew 6:26
            ["khkm"] = 40006031, // Matthew 6:31
            ["dhmn"] = 40006033, // Matthew 6:33
            ["mkak"] = 40007007, // Matthew 7:7
            ["sabh"] = 40007008, // Matthew 7:8
            ["knln"] = 40007012, // Matthew 7:12
            ["msak"] = 40011028, // Matthew 11:28
            ["nmsn"] = 40016024, // Matthew 16:24
            ["apnj"] = 40019026, // Matthew 19:26
            ["alnm"] = 40021022, // Matthew 21:22
            ["assk"] = 40022037, // Matthew 22:37
            ["iada"] = 40022038, // Matthew 22:38
            ["aapk"] = 40022039, // Matthew 22:39
            ["dkpn"] = 40024029, // NEW: Matthew 24:29
            ["spni"] = 40024032, // NEW: Matthew 24:32
            ["alaa"] = 40024035, // Matthew 24:35
            ["ntsa"] = 40024036, // NEW: Matthew 24:36
            ["akpa"] = 40024037, // NEW: Matthew 24:37
            ["kmsb"] = 40024040, // NEW: Matthew 24:40
            ["dbns"] = 40024041, // NEW: Matthew 24:41
            ["ditn"] = 40024043, // NEW: Matthew 24:43
            ["knkm"] = 40024044, // NEW: Matthew 24:44
            ["asah"] = 40025040, // Matthew 25:40
            ["swrs"] = 40028006, // NEW: Matthew 28:6
            ["amkm"] = 40028007, // NEW: Matthew 28:7
            ["alsj"] = 40028018, // Matthew 28:18
            ["ddmn"] = 40028019, // Matthew 28:19
            ["nins"] = 40028020, // Matthew 28:20
            ["apne"] = 41001001, // Mark 1:1
            ["aspk"] = 41001002, // NEW: Mark 1:2
            ["dsjn"] = 41001004, // NEW: Mark 1:4
            ["annm"] = 41001009, // NEW: Mark 1:9
            ["asnn"] = 41001015, // NEW: Mark 1:15
            ["anim"] = 41002017, // NEW: Mark 2:17
            ["dhpn"] = 41005036, // NEW: Mark 5:36
            ["psaa"] = 41009024, // NEW: Mark 9:24
            ["ansu"] = 41010017, // Mark 10:17
            ["nmam"] = 41010019, // Mark 10:19
            ["apsk"] = 41010021, // Mark 10:21
            ["dsns"] = 41010022, // Mark 10:22
            ["anam"] = 41010024, // Mark 10:24
            ["mpsi"] = 41010025, // Mark 10:25
            ["asnl"] = 41010026, // Mark 10:26
            ["pnjs"] = 41010027, // Mark 10:27
            ["snjk"] = 41010029, // Mark 10:29
            ["nhst"] = 41010030, // Mark 10:30
            ["dmna"] = 41010031, // Mark 10:31
            ["aska"] = 41010034, // Mark 10:34
            ["saan"] = 41010045, // Mark 10:45
            ["knsk"] = 41011024, // Mark 11:24
            ["aima"] = 41012030, // Mark 12:30
            ["apai"] = 41012031, // Mark 12:31
            ["asns"] = 41016015, // Mark 16:15
            ["alam"] = 41016017, // NEW: Mark 16:17
            ["smnm"] = 41016018, // NEW: Mark 16:18
            ["umma"] = 42001004, // NEW: Luke 1:4
            ["swsm"] = 42001037, // Luke 1:37
            ["amab"] = 42001045, // NEW: Luke 1:45
            ["anaa"] = 42001047, // NEW: Luke 1:47
            ["snna"] = 42001048, // NEW: Luke 1:48
            ["sisi"] = 42002011, // NEW: Luke 2:11
            ["lsds"] = 42002014, // NEW: Luke 2:14
            ["ssnt"] = 42006014, // Luke 6:14
            ["apnb"] = 42006019, // Luke 6:19
            ["aina"] = 42006020, // Luke 6:20
            ["mknn"] = 42006021, // Luke 6:21
            ["mkkk"] = 42006022, // Luke 6:22
            ["dsan"] = 42006024, // Luke 6:24
            ["sanm"] = 42006025, // Luke 6:25
            ["sanp"] = 42006026, // Luke 6:26
            ["pnas"] = 42006028, // Luke 6:28
            ["sssi"] = 42006029, // Luke 6:29
            ["akaa"] = 42006031, // Luke 6:31
            ["akkm"] = 42006032, // Luke 6:32
            ["dina"] = 42006035, // NEW: Luke 6:35
            ["mkgn"] = 42006036, // Luke 6:36
            ["apns"] = 42010027, // Luke 10:27
            ["asks"] = 42011009, // Luke 11:9
            ["dmam"] = 42012007, // Luke 12:7
            ["gmhn"] = 42012031, // Luke 12:31
            ["shnn"] = 42014027, // NEW: Luke 14:27
            ["sksi"] = 42015007, // NEW: Luke 15:7
            ["dsna"] = 42018027, // Luke 18:27
            ["nsak"] = 42022042, // NEW: Luke 22:42
            ["asnj"] = 42023034, // Luke 23:34
            ["asjn"] = 42023046, // Luke 23:46
            ["npsa"] = 43001001, // John 1:1
            ["anav"] = 43001014, // John 1:14
            ["psnj"] = 43001015, // John 1:15
            ["sskk"] = 43001016, // John 1:16
            ["sisp"] = 43001017, // John 1:17
            ["wtnk"] = 43001018, // John 1:18
            ["akia"] = 43001020, // John 1:20
            ["snns"] = 43001022, // John 1:22
            ["snaa"] = 43001023, // John 1:23
            ["assb"] = 43001024, // John 1:24
            ["ssnj"] = 43001026, // John 1:26
            ["snap"] = 43001027, // John 1:27
            ["nkan"] = 43001029, // John 1:29
            ["snki"] = 43002005, // NEW: John 2:5
            ["ssja"] = 43003003, // John 3:3
            ["sgnl"] = 43003016, // John 3:16
            ["shsn"] = 43003017, // John 3:17
            ["dasu"] = 43004014, // NEW: John 4:14
            ["adae"] = 43004024, // John 4:24
            ["kksk"] = 43005024, // NEW: John 5:24
            ["alni"] = 43006037, // NEW: John 6:37
            ["siak"] = 43006040, // NEW: John 6:40
            ["mnns"] = 43008012, // John 8:12
            ["snnj"] = 43008031, // John 8:31
            ["kpnk"] = 43008036, // NEW: John 8:36
            ["hpam"] = 43010010, // John 10:10
            ["aamp"] = 43010011, // John 10:11
            ["sskn"] = 43011025, // John 11:25
            ["aasn"] = 43011026, // John 11:26
            ["anni"] = 43012046, // NEW: John 12:46
            ["ibua"] = 43013034, // John 13:34
            ["hmai"] = 43014001, // John 14:1
            ["aaai"] = 43014013, // John 14:13
            ["kkmn"] = 43014014, // John 14:14
            ["kaii"] = 43014015, // John 14:15
            ["akai"] = 43014027, // John 14:27
            ["kmns"] = 43015003, // John 15:3
            ["kmsa"] = 43015004, // John 15:4
            ["aapn"] = 43015005, // John 15:5
            ["kkms"] = 43015007, // John 15:7
            ["iaau"] = 43015012, // John 15:12
            ["wmld"] = 43015013, // John 15:13
            ["ahnh"] = 43015016, // John 15:16
            ["hnwp"] = 43016024, // NEW: John 16:24
            ["ambn"] = 43016033, // John 16:33
            ["aiab"] = 43017003, // John 17:3
            ["pmss"] = 43017017, // John 17:17
            ["hlsa"] = 43017020, // NEW: John 17:20
            ["nami"] = 43020031, // NEW: John 20:31
            ["dtna"] = 44001008, // Acts 1:8
            ["nnnk"] = 44001011, // NEW: Acts 1:11
            ["anma"] = 44001015, // Acts 1:15
            ["mkkm"] = 44001016, // Acts 1:16
            ["ssis"] = 44001017, // Acts 1:17
            ["knat"] = 44001018, // Acts 1:18
            ["ains"] = 44001019, // Acts 1:19
            ["smti"] = 44001021, // Acts 1:21
            ["mspn"] = 44001022, // Acts 1:22
            ["utak"] = 44001025, // Acts 1:25
            ["andn"] = 44002001, // Acts 2:1
            ["abdm"] = 44002002, // Acts 2:2
            ["asla"] = 44002004, // Acts 2:4
            ["amna"] = 44002021, // NEW: Acts 2:21
            ["askm"] = 44004012, // Acts 4:12
            ["sgnn"] = 44009031, // NEW: Acts 9:31
            ["sapn"] = 44010043, // NEW: Acts 10:43
            ["aksm"] = 44016031, // Acts 16:31
            ["nlnm"] = 44017011, // NEW: Acts 17:11
            ["ukha"] = 44017027, // NEW: Acts 17:27
            ["sasa"] = 44018010, // NEW: Acts 18:10
            ["dhkm"] = 44020024, // NEW: Acts 20:24
            ["anik"] = 44020032, // NEW: Acts 20:32
            ["nhas"] = 44020035, // Acts 20:35
            ["uima"] = 44026018, // NEW: Acts 26:18
            ["shki"] = 45001016, // Romans 1:16
            ["samb"] = 45001020, // Romans 1:20
            ["akla"] = 45003013, // Romans 3:13
            ["akba"] = 45003014, // Romans 3:14
            ["akmp"] = 45003015, // Romans 3:15
            ["kaka"] = 45003016, // Romans 3:16
            ["aadn"] = 45003017, // Romans 3:17
            ["wpsd"] = 45003018, // Romans 3:18
            ["nann"] = 45003019, // Romans 3:19
            ["dnbs"] = 45003021, // Romans 3:21
            ["smba"] = 45003022, // Romans 3:22
            ["sala"] = 45003023, // Romans 3:23
            ["pinw"] = 45003024, // Romans 3:24
            ["nsin"] = 45003025, // Romans 3:25
            ["dind"] = 45005008, // Romans 5:8
            ["kkpn"] = 45005012, // NEW: Romans 5:12
            ["sksp"] = 45005017, // NEW: Romans 5:17
            ["tnnn"] = 45006004, // NEW: Romans 6:4
            ["sakn"] = 45006023, // Romans 6:23
            ["knak"] = 45007012, // Romans 7:12
            ["nnwn"] = 45008001, // Romans 8:1
            ["sama"] = 45008005, // NEW: Romans 8:5
            ["snkn"] = 45008018, // Romans 8:18
            ["agdn"] = 45008026, // Romans 8:26
            ["annn"] = 45008028, // Romans 8:28
            ["aaas"] = 45008031, // Romans 8:31
            ["snhi"] = 45008032, // Romans 8:32
            ["hksl"] = 45008037, // Romans 8:37
            ["sanl"] = 45008038, // Romans 8:38
            ["kakk"] = 45008039, // Romans 8:39
            ["ssca"] = 45010004, // Romans 10:4
            ["skim"] = 45010009, // Romans 10:9
            ["satn"] = 45010010, // Romans 10:10
            ["knap"] = 45010017, // Romans 10:17
            ["oknm"] = 45011033, // NEW: Romans 11:33
            ["knmk"] = 45012001, // Romans 12:1
            ["ahkm"] = 45012002, // Romans 12:2
            ["mspm"] = 45012012, // Romans 12:12
            ["kmas"] = 45012018, // NEW: Romans 12:18
            ["hkmm"] = 45012019, // NEW: Romans 12:19
            ["hkps"] = 45012021, // Romans 12:21
            ["abka"] = 45013001, // NEW: Romans 13:1
            ["sknt"] = 45014008, // NEW: Romans 14:8
            ["lnnd"] = 45015005, // NEW: Romans 15:5
            ["pnkn"] = 45015013, // Romans 15:13
            ["nbsc"] = 46001013, // 1 Corinthians 1:13
            ["nasd"] = 46001014, // 1 Corinthians 1:14
            ["abkr"] = 46001016, // 1 Corinthians 1:16
            ["shas"] = 46001017, // 1 Corinthians 1:17
            ["sasn"] = 46001018, // 1 Corinthians 1:18
            ["snik"] = 46001019, // 1 Corinthians 1:19
            ["snam"] = 46001020, // 1 Corinthians 1:20
            ["sysk"] = 46001021, // 1 Corinthians 1:21
            ["amjn"] = 46001022, // 1 Corinthians 1:22
            ["daai"] = 46001023, // 1 Corinthians 1:23
            ["kpnd"] = 46001027, // 1 Corinthians 1:27
            ["uwln"] = 46001029, // 1 Corinthians 1:29
            ["hbnn"] = 46003016, // 1 Corinthians 3:16
            ["kgns"] = 46003017, // NEW: 1 Corinthians 3:17
            ["ohbn"] = 46006019, // 1 Corinthians 6:19
            ["hdsi"] = 46010013, // 1 Corinthians 10:13
            ["kkkn"] = 46010031, // NEW: 1 Corinthians 10:31
            ["mttk"] = 46011001, // NEW: 1 Corinthians 11:1
            ["dsbi"] = 46012007, // NEW: 1 Corinthians 12:7
            ["kamn"] = 46013001, // NEW: 1 Corinthians 13:1
            ["akma"] = 46013002, // NEW: 1 Corinthians 13:2
            ["hnmh"] = 46013005, // NEW: 1 Corinthians 13:5
            ["hnsk"] = 46013006, // NEW: 1 Corinthians 13:6
            ["labl"] = 46013007, // 1 Corinthians 13:7
            ["kinn"] = 46014037, // 1 Corinthians 14:37
            ["hkpa"] = 46015033, // 1 Corinthians 15:33
            ["mkmk"] = 46016013, // NEW: 1 Corinthians 16:13
            ["gnsp"] = 46016014, // NEW: 1 Corinthians 16:14
            ["smgm"] = 47001020, // NEW: 2 Corinthians 1:20
            ["dtln"] = 47003018, // NEW: 2 Corinthians 3:18
            ["ntna"] = 47004007, // 2 Corinthians 4:7
            ["saln"] = 47004015, // 2 Corinthians 4:15
            ["skan"] = 47004018, // 2 Corinthians 4:18
            ["snnn"] = 47005001, // 2 Corinthians 5:1
            ["stns"] = 47005002, // 2 Corinthians 5:2
            ["nkmn"] = 47005003, // 2 Corinthians 5:3
            ["stnk"] = 47005004, // 2 Corinthians 5:4
            ["nags"] = 47005005, // 2 Corinthians 5:5
            ["knkl"] = 47005006, // 2 Corinthians 5:6
            ["snks"] = 47005007, // 2 Corinthians 5:7
            ["nmal"] = 47005008, // 2 Corinthians 5:8
            ["stla"] = 47005010, // 2 Corinthians 5:10
            ["hnim"] = 47005012, // 2 Corinthians 5:12
            ["kkas"] = 47005017, // 2 Corinthians 5:17
            ["sakm"] = 47007010, // NEW: 2 Corinthians 7:10
            ["dska"] = 47009006, // NEW: 2 Corinthians 9:6
            ["amgn"] = 47009008, // NEW: 2 Corinthians 9:8
            ["knan"] = 47012010, // NEW: 2 Corinthians 12:10
            ["tnam"] = 47012012, // NEW: 2 Corinthians 12:12
            ["aagn"] = 47012015, // NEW: 2 Corinthians 12:15
            ["abnp"] = 47013014, // NEW: 2 Corinthians 13:14
            ["spna"] = 48001001, // NEW: Galatians 1:1
            ["snab"] = 48001003, // NEW: Galatians 1:3
            ["nihi"] = 48001007, // NEW: Galatians 1:7
            ["dkko"] = 48001008, // NEW: Galatians 1:8
            ["sabn"] = 48001010, // NEW: Galatians 1:10
            ["sais"] = 48001011, // NEW: Galatians 1:11
            ["sbna"] = 48002012, // Galatians 2:12
            ["aaim"] = 48002013, // Galatians 2:13
            ["nnam"] = 48002014, // Galatians 2:14
            ["tmjs"] = 48002015, // Galatians 2:15
            ["nksa"] = 48002017, // Galatians 2:17
            ["kanm"] = 48002018, // Galatians 2:18
            ["sasp"] = 48002019, // Galatians 2:19
            ["ansk"] = 48002020, // Galatians 2:20
            ["hknn"] = 48002021, // Galatians 2:21
            ["wmjo"] = 48003028, // NEW: Galatians 3:28
            ["aphk"] = 48004007, // NEW: Galatians 4:7
            ["skap"] = 48005001, // Galatians 5:1
            ["nasp"] = 48005002, // Galatians 5:2
            ["opkm"] = 48005003, // Galatians 5:3
            ["skcj"] = 48005006, // NEW: Galatians 5:6
            ["sabk"] = 48005014, // NEW: Galatians 5:14
            ["dabn"] = 48005022, // Galatians 5:22
            ["kpls"] = 48005023, // Galatians 5:23
            ["mksp"] = 48006002, // Galatians 6:2
            ["ahtm"] = 48006009, // Galatians 6:9
            ["knst"] = 48006010, // Galatians 6:10
            ["dmns"] = 48006014, // Galatians 6:14
            ["nskm"] = 49001007, // NEW: Ephesians 1:7
            ["ddar"] = 49001015, // Ephesians 1:15
            ["ahat"] = 49001016, // Ephesians 1:16
            ["uisi"] = 49001017, // Ephesians 1:17
            ["nkgk"] = 49001020, // Ephesians 1:20
            ["sknl"] = 49001021, // Ephesians 1:21
            ["aaln"] = 49001022, // Ephesians 1:22
            ["akbn"] = 49002001, // Ephesians 2:1
            ["ninn"] = 49002002, // Ephesians 2:2
            ["sgnm"] = 49002003, // Ephesians 2:3
            ["nadp"] = 49002004, // Ephesians 2:4
            ["atik"] = 49002006, // Ephesians 2:6
            ["ssbk"] = 49002008, // Ephesians 2:8
            ["stkg"] = 49002010, // NEW: Ephesians 2:10
            ["usii"] = 49003016, // NEW: Ephesians 3:16
            ["nsca"] = 49003017, // NEW: Ephesians 3:17
            ["nsmg"] = 49003020, // Ephesians 3:20
            ["nbka"] = 49004002, // NEW: Ephesians 4:2
            ["kmah"] = 49004026, // NEW: Ephesians 4:26
            ["amks"] = 49004032, // Ephesians 4:32
            ["knms"] = 49005001, // NEW: Ephesians 5:1
            ["snpk"] = 49005008, // NEW: Ephesians 5:8
            ["mlin"] = 49005025, // NEW: Ephesians 5:25
            ["gdnn"] = 49005028, // NEW: Ephesians 5:28
            ["skmk"] = 49006010, // Ephesians 6:10
            ["mknb"] = 49006011, // Ephesians 6:11
            ["saap"] = 49006012, // Ephesians 6:12
            ["nmks"] = 49006018, // Ephesians 6:18
            ["aiik"] = 50001009, // NEW: Philippians 1:9
            ["uika"] = 50001010, // NEW: Philippians 1:10
            ["nikn"] = 50001012, // Philippians 1:12
            ["aaks"] = 50001014, // Philippians 1:14
            ["tnin"] = 50001015, // Philippians 1:15
            ["aign"] = 50001016, // Philippians 1:16
            ["dini"] = 50001017, // Philippians 1:17
            ["angm"] = 50001018, // Philippians 1:18
            ["asam"] = 50001020, // Philippians 1:20
            ["ssga"] = 50001021, // Philippians 1:21
            ["gmam"] = 50001024, // Philippians 1:24
            ["aspi"] = 50001025, // Philippians 1:25
            ["nhng"] = 50002003, // NEW: Philippians 2:3
            ["sdag"] = 50002013, // NEW: Philippians 2:13
            ["gnal"] = 50002014, // Philippians 2:14
            ["onal"] = 50003008, // NEW: Philippians 3:8
            ["nash"] = 50003014, // NEW: Philippians 3:14
            ["mkls"] = 50004004, // Philippians 4:4
            ["mnai"] = 50004005, // NEW: Philippians 4:5
            ["aakn"] = 50004007, // Philippians 4:7
            ["kmka"] = 50004008, // Philippians 4:8
            ["hssk"] = 50004011, // NEW: Philippians 4:11
            ["mama"] = 50004012, // NEW: Philippians 4:12
            ["lnmb"] = 50004013, // Philippians 4:13
            ["dman"] = 50004018, // NEW: Philippians 4:18
            ["nksd"] = 51001003, // NEW: Colossians 1:3
            ["nnsa"] = 51001012, // Colossians 1:12
            ["nskn"] = 51001014, // Colossians 1:14
            ["nsal"] = 51001015, // Colossians 1:15
            ["asus"] = 51001017, // Colossians 1:17
            ["asau"] = 51001018, // Colossians 1:18
            ["aknn"] = 51001021, // Colossians 1:21
            ["gmpn"] = 51001022, // Colossians 1:22
            ["ktnk"] = 51001023, // Colossians 1:23
            ["nnas"] = 51001024, // Colossians 1:24
            ["nagm"] = 51001025, // Colossians 1:25
            ["umak"] = 51002002, // NEW: Colossians 2:2
            ["kpnn"] = 51002006, // NEW: Colossians 2:6
            ["nnan"] = 51002007, // NEW: Colossians 2:7
            ["kknm"] = 51003001, // NEW: Colossians 3:1
            ["inai"] = 51003002, // Colossians 3:2
            ["mnkg"] = 51003012, // NEW: Colossians 3:12
            ["mnss"] = 51003016, // Colossians 3:16
            ["aaig"] = 51003017, // NEW: Colossians 3:17
            ["mamk"] = 51003022, // Colossians 3:22
            ["aiga"] = 51003023, // NEW: Colossians 3:23
            ["mkps"] = 51004002, // Colossians 4:2
            ["aipn"] = 51004006, // NEW: Colossians 4:6
            ["nkls"] = 52001002, // 1 Thessalonians 1:2
            ["naiw"] = 52001003, // NEW: 1 Thessalonians 1:3
            ["adnd"] = 52002013, // 1 Thessalonians 2:13
            ["uash"] = 52003003, // NEW: 1 Thessalonians 3:3
            ["ddnn"] = 52003005, // NEW: 1 Thessalonians 3:5
            ["sttn"] = 52004007, // 1 Thessalonians 4:7
            ["kanh"] = 52004008, // NEW: 1 Thessalonians 4:8
            ["ukmn"] = 52004012, // NEW: 1 Thessalonians 4:12
            ["sisn"] = 52004015, // 1 Thessalonians 4:15
            ["sapd"] = 52004016, // 1 Thessalonians 4:16
            ["kmtn"] = 52004017, // 1 Thessalonians 4:17
            ["kmks"] = 52004018, // 1 Thessalonians 4:18
            ["dtsm"] = 52005001, // 1 Thessalonians 5:1
            ["skra"] = 52005002, // 1 Thessalonians 5:2
            ["psnm"] = 52005003, // 1 Thessalonians 5:3
            ["nkmk"] = 52005004, // 1 Thessalonians 5:4
            ["skla"] = 52005005, // 1 Thessalonians 5:5
            ["hntm"] = 52005006, // 1 Thessalonians 5:6
            ["ddkm"] = 52005011, // NEW: 1 Thessalonians 5:11
            ["mkwp"] = 52005017, // 1 Thessalonians 5:17
            ["slnm"] = 52005018, // 1 Thessalonians 5:18
            ["snal"] = 52005021, // 1 Thessalonians 5:21
            ["lnab"] = 52005022, // 1 Thessalonians 5:22
            ["apkl"] = 52005023, // NEW: 1 Thessalonians 5:23
            ["tysi"] = 52005024, // NEW: 1 Thessalonians 5:24
            ["mkin"] = 52005025, // NEW: 1 Thessalonians 5:25
            ["apks"] = 53001004, // NEW: 2 Thessalonians 1:4
            ["nith"] = 53001005, // NEW: 2 Thessalonians 1:5
            ["kibn"] = 53001006, // NEW: 2 Thessalonians 1:6
            ["ddda"] = 53001011, // NEW: 2 Thessalonians 1:11
            ["uapn"] = 53001012, // NEW: 2 Thessalonians 1:12
            ["hkpk"] = 53002003, // NEW: 2 Thessalonians 2:3
            ["ambd"] = 53002010, // NEW: 2 Thessalonians 2:10
            ["naap"] = 53002016, // NEW: 2 Thessalonians 2:16
            ["ntap"] = 53003003, // 2 Thessalonians 3:3
            ["nnmk"] = 53003008, // NEW: 2 Thessalonians 3:8
            ["snpm"] = 53003010, // NEW: 2 Thessalonians 3:10
            ["nakn"] = 54001005, // NEW: 1 Timothy 1:5
            ["nnmm"] = 54001007, // 1 Timothy 1:7
            ["atsa"] = 54001014, // 1 Timothy 1:14
            ["tapa"] = 54001015, // 1 Timothy 1:15
            ["gmdd"] = 54001016, // 1 Timothy 1:16
            ["nshw"] = 54001017, // 1 Timothy 1:17
            ["abia"] = 54001018, // 1 Timothy 1:18
            ["nima"] = 54001019, // 1 Timothy 1:19
            ["nsmi"] = 54001020, // 1 Timothy 1:20
            ["unsl"] = 54002001, // 1 Timothy 2:1
            ["amha"] = 54002002, // 1 Timothy 2:2
            ["iman"] = 54002003, // 1 Timothy 2:3
            ["smid"] = 54002005, // 1 Timothy 2:5
            ["niak"] = 54002006, // 1 Timothy 2:6
            ["ikna"] = 54002008, // NEW: 1 Timothy 2:8
            ["tapk"] = 54003001, // NEW: 1 Timothy 3:1
            ["nkam"] = 54003015, // 1 Timothy 3:15
            ["awpd"] = 54003016, // NEW: 1 Timothy 3:16
            ["spsp"] = 54004005, // 1 Timothy 4:5
            ["hhns"] = 54004012, // NEW: 1 Timothy 4:12
            ["dkas"] = 54005008, // NEW: 1 Timothy 5:8
            ["ammn"] = 54005017, // NEW: 1 Timothy 5:17
            ["saps"] = 54006010, // NEW: 1 Timothy 6:10
            ["amss"] = 54006017, // NEW: 1 Timothy 6:17
            ["ktna"] = 55001002, // NEW: 2 Timothy 1:2
            ["nkkm"] = 55001004, // NEW: 2 Timothy 1:4
            ["nika"] = 55001005, // NEW: 2 Timothy 1:5
            ["ddai"] = 55001006, // NEW: 2 Timothy 1:6
            ["shtb"] = 55001007, // 2 Timothy 1:7
            ["hmni"] = 55001008, // NEW: 2 Timothy 1:8
            ["inmn"] = 55001015, // 2 Timothy 1:15
            ["knsn"] = 55001017, // 2 Timothy 1:17
            ["pnsn"] = 55001018, // 2 Timothy 1:18
            ["inak"] = 55002001, // 2 Timothy 2:1
            ["aamb"] = 55002002, // 2 Timothy 2:2
            ["sknn"] = 55002004, // 2 Timothy 2:4
            ["akas"] = 55002005, // 2 Timothy 2:5
            ["amnn"] = 55002006, // 2 Timothy 2:6
            ["imas"] = 55002007, // 2 Timothy 2:7
            ["amsj"] = 55002008, // 2 Timothy 2:8
            ["pmhn"] = 55002015, // 2 Timothy 2:15
            ["kasn"] = 55002021, // NEW: 2 Timothy 2:21
            ["amsp"] = 55003015, // NEW: 2 Timothy 3:15
            ["uatn"] = 55003017, // NEW: 2 Timothy 3:17
            ["nanm"] = 55004007, // 2 Timothy 4:7
            ["ainp"] = 55004018, // NEW: 2 Timothy 4:18
            ["spsb"] = 56001002, // NEW: Titus 1:2
            ["apia"] = 56001013, // Titus 1:13
            ["nhms"] = 56001014, // Titus 1:14
            ["smal"] = 56001015, // Titus 1:15
            ["nmkn"] = 56002001, // Titus 2:1
            ["naml"] = 56002002, // Titus 2:2
            ["ngda"] = 56002003, // Titus 2:3
            ["ukma"] = 56002004, // Titus 2:4
            ["mmms"] = 56002005, // Titus 2:5
            ["imrn"] = 56002006, // Titus 2:6
            ["pnmn"] = 56002008, // Titus 2:8
            ["nhym"] = 56002013, // NEW: Titus 2:13
            ["imsk"] = 56003001, // NEW: Titus 3:1
            ["nhmn"] = 56003002, // NEW: Titus 3:2
            ["strn"] = 56003003, // NEW: Titus 3:3
            ["nnmn"] = 56003004, // NEW: Titus 3:4
            ["nhds"] = 56003005, // NEW: Titus 3:5
            ["nkin"] = 56003006, // NEW: Titus 3:6
            ["usps"] = 56003007, // NEW: Titus 3:7
            ["spnb"] = 57001001, // NEW: Philemon 1:1
            ["nals"] = 57001004, // NEW: Philemon 1:4
            ["spkn"] = 57001005, // NEW: Philemon 1:5
            ["kbkc"] = 57001008, // NEW: Philemon 1:8
            ["gmas"] = 57001009, // NEW: Philemon 1:9
            ["nnup"] = 57001011, // NEW: Philemon 1:11
            ["nsap"] = 57001012, // NEW: Philemon 1:12
            ["niks"] = 57001013, // NEW: Philemon 1:13
            ["dkwk"] = 57001014, // NEW: Philemon 1:14
            ["smsg"] = 57001015, // Philemon 1:15
            ["nhna"] = 57001016, // Philemon 1:16
            ["kana"] = 57001017, // Philemon 1:17
            ["nksn"] = 57001018, // Philemon 1:18
            ["okmn"] = 57001020, // Philemon 1:20
            ["kssp"] = 57001021, // Philemon 1:21
            ["dbal"] = 57001022, // Philemon 1:22
            ["abna"] = 57001025, // Philemon 1:25
            ["knnp"] = 58002001, // NEW: Hebrews 2:1
            ["dscg"] = 58003006, // NEW: Hebrews 3:6
            ["mntn"] = 58004011, // Hebrews 4:11
            ["awan"] = 58004013, // Hebrews 4:13
            ["stwi"] = 58004015, // Hebrews 4:15
            ["mntm"] = 58004016, // Hebrews 4:16
            ["sada"] = 58006010, // NEW: Hebrews 6:10
            ["ddns"] = 58007025, // NEW: Hebrews 7:25
            ["naim"] = 58010023, // NEW: Hebrews 10:23
            ["atmu"] = 58010024, // NEW: Hebrews 10:24
            ["napa"] = 58011001, // Hebrews 11:1
            ["sspn"] = 58011002, // Hebrews 11:2
            ["span"] = 58011003, // Hebrews 11:3
            ["spsa"] = 58011004, // Hebrews 11:4
            ["spse"] = 58011005, // Hebrews 11:5
            ["akwp"] = 58011006, // Hebrews 11:6
            ["spsn"] = 58011007, // Hebrews 11:7
            ["sina"] = 58011010, // Hebrews 11:10
            ["aspa"] = 58011013, // NEW: Hebrews 11:13
            ["kynt"] = 58012001, // Hebrews 12:1
            ["nmns"] = 58012002, // Hebrews 12:2
            ["sjas"] = 58013008, // NEW: Hebrews 13:8
            ["dapn"] = 58013016, // NEW: Hebrews 13:16
            ["mkka"] = 59001002, // NEW: James 1:2
            ["nknn"] = 59001005, // James 1:5
            ["nhsm"] = 59001006, // James 1:6
            ["kmak"] = 59001015, // James 1:15
            ["hkpm"] = 59001016, // James 1:16
            ["abmk"] = 59001017, // James 1:17
            ["sksk"] = 59001018, // James 1:18
            ["nnim"] = 59001019, // James 1:19
            ["sagn"] = 59001020, // James 1:20
            ["kina"] = 59001021, // James 1:21
            ["dmtk"] = 59001022, // James 1:22
            ["skas"] = 59001023, // James 1:23
            ["kasa"] = 59001026, // James 1:26
            ["adnr"] = 59001027, // James 1:27
            ["mkky"] = 59002001, // James 2:1
            ["skmp"] = 59002002, // James 2:2
            ["dnmm"] = 59002005, // NEW: James 2:5
            ["nakb"] = 59003017, // NEW: James 3:17
            ["aabn"] = 59003018, // NEW: James 3:18
            ["pnks"] = 59004007, // James 4:7
            ["mksd"] = 59004008, // James 4:8
            ["iatn"] = 59004012, // NEW: James 4:12
            ["ntnm"] = 59005011, // NEW: James 5:11
            ["nhsl"] = 59005012, // NEW: James 5:12
            ["nbas"] = 59005013, // James 5:13
            ["msba"] = 59005014, // NEW: James 5:14
            ["mnks"] = 59005016, // James 5:16
            ["aann"] = 59005020, // NEW: James 5:20
            ["pnad"] = 60001003, // NEW: 1 Peter 1:3
            ["niai"] = 60001006, // NEW: 1 Peter 1:6
            ["uaps"] = 60001007, // NEW: 1 Peter 1:7
            ["nisk"] = 60001012, // 1 Peter 1:12
            ["kiba"] = 60001013, // 1 Peter 1:13
            ["ngnm"] = 60001014, // 1 Peter 1:14
            ["nyba"] = 60001015, // 1 Peter 1:15
            ["snkm"] = 60001016, // 1 Peter 1:16
            ["akit"] = 60001017, // 1 Peter 1:17
            ["nink"] = 60001018, // 1 Peter 1:18
            ["knmd"] = 60001019, // 1 Peter 1:19
            ["nnnn"] = 60001020, // 1 Peter 1:20
            ["nspn"] = 60001021, // 1 Peter 1:21
            ["gnms"] = 60002002, // NEW: 1 Peter 2:2
            ["dkil"] = 60002009, // 1 Peter 2:9
            ["mmik"] = 60002011, // NEW: 1 Peter 2:11
            ["nsra"] = 60002024, // NEW: 1 Peter 2:24
            ["kkla"] = 60003008, // NEW: 1 Peter 3:8
            ["kiab"] = 60003015, // 1 Peter 3:15
            ["nusl"] = 60004008, // NEW: 1 Peter 4:8
            ["nask"] = 60004010, // NEW: 1 Peter 4:10
            ["niis"] = 60005007, // 1 Peter 5:7
            ["kmmk"] = 60005008, // 1 Peter 5:8
            ["ndda"] = 61001004, // NEW: 2 Peter 1:4
            ["oadd"] = 61001005, // NEW: 2 Peter 1:5
            ["skni"] = 61001008, // NEW: 2 Peter 1:8
            ["sywn"] = 61001009, // NEW: 2 Peter 1:9
            ["kmkl"] = 61001010, // NEW: 2 Peter 1:10
            ["kmha"] = 61001012, // NEW: 2 Peter 1:12
            ["aikm"] = 61001013, // 2 Peter 1:13
            ["opkn"] = 61001015, // 2 Peter 1:15
            ["skah"] = 61001016, // 2 Peter 1:16
            ["ssts"] = 61001017, // 2 Peter 1:17
            ["aatn"] = 61001018, // 2 Peter 1:18
            ["akam"] = 61001019, // 2 Peter 1:19
            ["nmmi"] = 61001020, // 2 Peter 1:20
            ["shsk"] = 61001021, // 2 Peter 1:21
            ["nmnd"] = 61002001, // 2 Peter 2:1
            ["amms"] = 61002002, // 2 Peter 2:2
            ["skpn"] = 61002020, // NEW: 2 Peter 2:20
            ["dhnk"] = 61003008, // NEW: 2 Peter 3:8
            ["hmap"] = 61003009, // NEW: 2 Peter 3:9
            ["knmm"] = 61003014, // NEW: 2 Peter 3:14
            ["nktn"] = 62001007, // NEW: 1 John 1:7
            ["ksnt"] = 62001008, // NEW: 1 John 1:8
            ["mkma"] = 62002001, // NEW: 1 John 2:1
            ["asap"] = 62002002, // NEW: 1 John 2:2
            ["dast"] = 62002005, // NEW: 1 John 2:5
            ["ansn"] = 62002006, // 1 John 2:6
            ["mmwa"] = 62002007, // 1 John 2:7
            ["mibu"] = 62002008, // 1 John 2:8
            ["ausk"] = 62002010, // 1 John 2:10
            ["nans"] = 62002011, // 1 John 2:11
            ["kskm"] = 62002012, // 1 John 2:12
            ["kasm"] = 62002014, // 1 John 2:14
            ["aasa"] = 62002017, // 1 John 2:17
            ["mmai"] = 62002018, // 1 John 2:18
            ["hkks"] = 62002021, // 1 John 2:21
            ["dnna"] = 62003016, // 1 John 3:16
            ["dasm"] = 62003017, // 1 John 3:17
            ["mmmt"] = 62004007, // 1 John 4:7
            ["ahua"] = 62004008, // 1 John 4:8
            ["wtsp"] = 62004018, // NEW: 1 John 4:18
            ["siap"] = 62005003, // NEW: 1 John 5:3
            ["sasi"] = 62005004, // NEW: 1 John 5:4
            ["asad"] = 62005005, // NEW: 1 John 5:5
            ["aiap"] = 62005011, // 1 John 5:11
            ["akna"] = 62005012, // 1 John 5:12
            ["aian"] = 62005014, // 1 John 5:14
            ["lnka"] = 62005017, // 1 John 5:17
            ["amsh"] = 63001001, // NEW: 2 John 1:1
            ["alnn"] = 63001004, // NEW: 2 John 1:4
            ["ksid"] = 63001010, // NEW: 2 John 1:10
            ["sabs"] = 63001011, // NEW: 2 John 1:11
            ["aman"] = 63001013, // NEW: 2 John 1:13
            ["amkg"] = 64001001, // NEW: 3 John 1:1
            ["main"] = 64001002, // NEW: 3 John 1:2
            ["wndk"] = 64001004, // NEW: 3 John 1:4
            ["mgma"] = 64001005, // NEW: 3 John 1:5
            ["sdsp"] = 64001007, // NEW: 3 John 1:7
            ["nnnt"] = 64001008, // NEW: 3 John 1:8
            ["kkpa"] = 64001010, // NEW: 3 John 1:10
            ["mhmt"] = 64001011, // NEW: 3 John 1:11
            ["sdap"] = 64001012, // NEW: 3 John 1:12
            ["mmba"] = 64001013, // 3 John 1:13
            ["dikm"] = 64001014, // 3 John 1:14
            ["smit"] = 65001004, // NEW: Jude 1:4
            ["nkni"] = 65001005, // NEW: Jude 1:5
            ["gdas"] = 65001007, // NEW: Jude 1:7
            ["daam"] = 65001009, // NEW: Jude 1:9
            ["dami"] = 65001010, // NEW: Jude 1:10
            ["sans"] = 65001011, // Jude 1:11
            ["amip"] = 65001012, // Jude 1:12
            ["mmna"] = 65001013, // Jude 1:13
            ["aami"] = 65001014, // Jude 1:14
            ["uiap"] = 65001015, // Jude 1:15
            ["amim"] = 65001016, // Jude 1:16
            ["nkmm"] = 65001017, // Jude 1:17
            ["kpss"] = 65001018, // Jude 1:18
            ["amia"] = 65001019, // Jude 1:19
            ["aain"] = 65001022, // Jude 1:22
            ["aaii"] = 65001023, // NEW: Jude 1:23
            ["ndsm"] = 65001024, // NEW: Jude 1:24
            ["sidn"] = 65001025, // NEW: Jude 1:25
            ["amkj"] = 66001005, // NEW: Revelation 1:5
            ["aaaa"] = 66001008, // Revelation 1:8
            ["aalu"] = 66001012, // Revelation 1:12
            ["asgn"] = 66001013, // Revelation 1:13
            ["aaku"] = 66001014, // Revelation 1:14
            ["aakm"] = 66001015, // Revelation 1:15
            ["askk"] = 66001016, // Revelation 1:16
            ["ansa"] = 66001017, // Revelation 1:17
            ["imna"] = 66001019, // Revelation 1:19
            ["ahnp"] = 66001020, // Revelation 1:20
            ["hmka"] = 66002010, // NEW: Revelation 2:10
            ["asan"] = 66003001, // Revelation 3:1
            ["mkap"] = 66003002, // Revelation 3:2
            ["amad"] = 66003005, // NEW: Revelation 3:5
            ["amag"] = 66003012, // NEW: Revelation 3:12
            ["mkop"] = 66004011, // NEW: Revelation 4:11
            ["nnnm"] = 66005012, // NEW: Revelation 5:12
            ["annt"] = 66007010, // NEW: Revelation 7:10
            ["sacn"] = 66007017, // NEW: Revelation 7:17
            ["askd"] = 66012011, // NEW: Revelation 12:11
            ["apna"] = 66021004, // Revelation 21:4
            ["aaea"] = 66022017, // NEW: Revelation 22:17
            ["assm"] = 66022020, // Revelation 22:20
        };



        private static readonly Dictionary<int, string> ReverseSCommands = SCommands.ToDictionary(x => x.Value, x => x.Key);

        public string AliasForThisVerse(int encodedBookValue)
        {
            if (ReverseSCommands.TryGetValue(encodedBookValue, out string alias))
            {
                string result = "S-state command: " + alias;
                return result;
            }
            else
            {
                return "S-state command: No alias found";
            }
        }

        
        private static readonly HashSet<string> SPrefixes = SemiColonPrefixes();

        private static HashSet<string> SemiColonPrefixes()
        {
            var prefixes = new HashSet<string>();

            foreach (var command in SCommands.Keys)
            {
                for (int i = 1; i < command.Length; i++)
                {
                    prefixes.Add(command[..i]);
                }
            }

            return prefixes;
        }

        public string ModeString()
        {
            if (state == State.B) return "BOOK Mode";
            else if (state == State.S) return "SEMI-COLON Mode";
            else if (state == State.C) return "CHAPTER Mode";
            else if (state == State.V) return "VERSE Mode";
            else if (state == State.Filter) return "SEARCH: FILTER mode";
            else if (state == State.Query) return "SEARCH: QUERY mode";
            else if (state == State.Choice)
            {
                if (normalChoosingMode)
                {
                    return "SEARCH: CHOICE mode";
                }
                else
                {
                    return "SEARCH: ABSOLUTE PATH mode";
                }
            }
            else return "Random mode";
        }

        private enum ParseState
        {
            Start,
            ReadingQuantifier,
            ReadingBookAlias,
            ReadingSemicolonAlias,
            SettingMark,
            TeleportingToMark,
            CopyingSomething,
            FilterDeciding,
            FilterBookAlias,
            FilterQuantifier,
        }

        public void ResetCommand()
        {
            parseState = ParseState.Start;
            quantifier = 0;
            this.command = null;
            commandBuffer = "";
        }

        private EntireCommandStatus NotValid()
        {
            validity = EntireCommandStatus.Invalid;
            ResetCommand();
            return validity;
        }

        public Command? TakeCommand()
        {
            Command? result = command;
            ResetCommand();
            return result;
        }


        //MAIN INPUT HANDLER
        public EntireCommandStatus Feed(char k)
        {
            AddCharToCommandInfo(k);
            //termination keys
            if (parseState == ParseState.TeleportingToMark)
            {
                command = new Command { Mode = State.S, Quantity = -1 * (int)k, Direction = Direct.Absolute };
                validity = EntireCommandStatus.Valid;
                return validity;
            }
            if (state == State.Query || state == State.Filter || state == State.Choice)
            {
                return Search(k);
            }
            if (k == 'x' && state != State.B && state != State.S)
            {
                ResetCommand();
                validity = EntireCommandStatus.Invalid;
                return validity;
            }
            else if (k == ' ')
            {
                if (state == State.C || state == State.V)
                {
                    return CompleteMovement(Direct.Absolute);
                }

                ResetCommand();
                validity = EntireCommandStatus.Invalid;
                return validity;
            }
            else if (k == '\'')
            {
                parseState = ParseState.TeleportingToMark;
                validity = EntireCommandStatus.Incomplete;
                return validity;
            }
            //book mode
            if (state == State.B)
            {
                commandBuffer += k;

                if (BCommands.TryGetValue(commandBuffer, out int position))
                {
                    command = new Command { Mode = State.B, Quantity = position, Direction = Direct.Absolute };
                    commandBuffer = "";
                    parseState = ParseState.Start;
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }

                if (BPrefixes.Contains(commandBuffer))
                {
                    parseState = ParseState.ReadingBookAlias;
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }
                
                //movement
                if (k == 'j' || k == 'k')
                {
                    return FeedMovement(k);
                }

                //mode switching in book state
                else if (k == 'c')
                {
                    state = State.C;
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }
                else if (k == 'v')
                {
                    state = State.V;
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }
                else if (k == ';')
                {
                    state = State.S;
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }
                else if (k == '/')
                {
                    previousStateBeforeSearch = state;
                    state = State.Query;
                    EnterQueryState();
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }
                else
                {
                    return NotValid();
                }
            }

            //mode switching
            if (parseState == ParseState.Start)
            {
                switch(k)
                {
                    case 'b':
                        state = State.B;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    case 'c':
                        state = State.C;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    case 'v':
                        state = State.V;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    case ';':
                        state = State.S;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    case '/':
                        previousStateBeforeSearch = state;
                        state = State.Query;
                        EnterQueryState() ;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    default:
                        break;
                }
            }

            //semi colon mode
            if (state == State.S)
            {
                commandBuffer += k;

                //checks if it is calling mark functions
                if (parseState == ParseState.SettingMark)
                {
                    command = new Command { Mode = state, Quantity = (int)k, Direction = Direct.Absolute };
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }
                else if (parseState == ParseState.CopyingSomething)
                {
                    switch (k)
                    {
                        case 't':
                            return CopyTheTitle();
                        case 'v':
                            return CopyTheVerse();
                        default:
                            return NotValid();
                    }
                }

                //trying to get S-state aliases
                if (SCommands.TryGetValue(commandBuffer, out int position))
                {
                    command = new Command
                    {
                        Mode = State.S,
                        Quantity = position,
                        Direction = Direct.Absolute
                    };

                    commandBuffer = "";
                    parseState = ParseState.Start;
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }

                if (SPrefixes.Contains(commandBuffer))
                {
                    parseState = ParseState.ReadingSemicolonAlias;
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }

                //One letter, sub-state switching commands
                if (parseState == ParseState.Start)
                {
                    switch (k)
                    {
                        case 'r':
                            return HistoryCommands(k);
                        case 'z':
                            return HistoryCommands(k);
                        case 'q':
                            parseState = ParseState.SettingMark;
                            validity = EntireCommandStatus.Incomplete;
                            return validity;
                        case 'x':
                            return TranslationChange();
                        case 'e':
                            return ToggleUI();
                        case 'f':
                            return HelpWasRequested();
                        case 'y':
                            parseState = ParseState.CopyingSomething;
                            validity = EntireCommandStatus.Incomplete;
                            return validity;
                        case '+':
                            return BackgroundChange();
                        default:
                            break;
                    }
                }

                return NotValid();
            }

            //chapter and verse mode
            if (state == State.C || state == State.V)
            {
                return FeedMovement(k);
            }

            //unexpected error has occurred (eg. a user pressed a random key)
            return NotValid();
        }

        private EntireCommandStatus BackgroundChange()
        {
            command = new Command { Mode = state, Direction = Direct.Absolute, Quantity = 700 };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private EntireCommandStatus Search(char k)
        {
            //toggles on and off SEARCH mode
            if (k == '/')
            {
                SearchUiVisibilityChanged?.Invoke(false); // Close search UI
                state = previousStateBeforeSearch;
                commandString = "";
                validity = EntireCommandStatus.Valid;
                return validity;
            }

            //tab character (switching focused ui's)
            else if (k == '+')
            {
                commandString = "";
                state = state switch
                {
                    State.Query => State.Filter,
                    State.Filter => State.Choice,
                    State.Choice => State.Query,
                    _ => State.Query
                };
                if (state == State.Query)
                {
                    EnterQueryState();
                }
                
                validity = EntireCommandStatus.Valid;
                return validity;
            }

            //query state
            if (state == State.Query)
            { 
                commandString = "/" + searchQuery;  
                if (k == '_')
                {
                    //process the search using whole query
                    state = State.Filter;
                    commandString = "";
                    AddFilter("", searchQuery, false);
                    return ShipOutProperSearchCommand();
                }
                else if (k == '{')
                {
                    if (searchQuery == "")
                    {
                        return NotValid();
                    }
                    searchQuery = searchQuery[..^1];
                    commandString = "/" + searchQuery;
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }
                else if (k == '<' || k == '>' || k == '|')
                {
                    validity = EntireCommandStatus.Invalid;
                    return validity;
                }
                else
                {
                    searchQuery += k;
                    commandString = "/" + searchQuery;
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }
            }

            //filter state
            else if (state == State.Filter)
            {
                if (k == '_')
                {
                    return ShipOutProperSearchCommand();
                }
                //choosing which filter the user wants to change
                if (parseState == ParseState.Start && activeFilter == null)
                {
                    switch (k)
                    {
                        case 'b':
                            parseState = ParseState.FilterBookAlias;
                            activeFilter = k;
                            break;
                        case 'c':
                            parseState = ParseState.FilterQuantifier;
                            activeFilter = k;
                            break;
                        case 'v':
                            parseState = ParseState.FilterQuantifier;
                            activeFilter = k;
                            break;
                        case 't':
                            parseState = ParseState.FilterDeciding;
                            activeFilter = k;
                            break;
                        case 'l':
                            parseState = ParseState.FilterDeciding;
                            activeFilter = k;
                            break;
                        case 'o':
                            parseState = ParseState.FilterDeciding;
                            activeFilter = k;
                            break;
                        case 'm':
                            parseState = ParseState.FilterDeciding;
                            activeFilter = k;
                            break;
                        default:
                            return NotValid();
                    }
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }

                //actually editing the filter now
                else if (activeFilter != null)
                {
                    if (parseState == ParseState.FilterBookAlias)
                    {
                        if (k == ' ')
                        {
                            AddFilter("b", " ", true);
                            validity = EntireCommandStatus.Valid;
                            return validity;
                        }
                        commandBuffer += k;

                        if (BCommands.TryGetValue(commandBuffer, out int position))
                        {
                            //adding and/or replacing book filter
                            string filterOption = position.ToString();
                            AddFilter("b", filterOption, false);

                            commandBuffer = "";
                            parseState = ParseState.Start;
                            validity = EntireCommandStatus.Valid;
                            return validity;
                        }

                        if (BPrefixes.Contains(commandBuffer))
                        {
                            validity = EntireCommandStatus.Incomplete;
                            return validity;
                        }
                        activeFilter = null;
                        return NotValid();
                    }
                    else if (parseState == ParseState.FilterQuantifier)
                    {
                        return FinishFilterQuantifier(k, activeFilter.ToString());
                    }
                    else if (parseState == ParseState.FilterDeciding)
                    {
                        switch (activeFilter)
                        {
                            case 't':
                                if (k == 'o' || k == 'n')
                                {
                                    AddFilter(activeFilter.ToString(), k.ToString(), false);
                                    validity = EntireCommandStatus.Valid;
                                    return validity;
                                }
                                else if (k == ' ')
                                {
                                    AddFilter(activeFilter.ToString(), k.ToString(), true);
                                    validity = EntireCommandStatus.Valid;
                                    return validity;
                                }
                                else
                                {
                                    activeFilter = null;
                                    return NotValid();
                                }
                            case 'l':
                                if (k == 'e' || k == 't')
                                {
                                    AddFilter(activeFilter.ToString(), k.ToString(), false);
                                    validity = EntireCommandStatus.Valid;
                                    return validity;
                                }
                                else
                                {
                                    activeFilter = null;
                                    return NotValid();
                                }
                            case 'o':
                                if (k == 'a' || k == 's')
                                {
                                    AddFilter(activeFilter.ToString(), k.ToString(), false);
                                    validity = EntireCommandStatus.Valid;
                                    return validity;
                                }
                                else
                                {
                                    activeFilter = null;
                                    return NotValid();
                                }
                            case 'm':
                                if (k == 'f' || k == 't')
                                {
                                    AddFilter(activeFilter.ToString(), k.ToString(), false);
                                    validity = EntireCommandStatus.Valid;
                                    return validity;
                                }
                                else
                                {
                                    activeFilter = null;
                                    return NotValid();
                                }
                        }
                    }
                    else
                    {
                        activeFilter = null;
                        validity = EntireCommandStatus.Invalid;
                        return validity;
                    }
                }
                activeFilter = null;
                return NotValid();
            }
            else if (state == State.Choice)
            {
                if (normalChoosingMode)
                {
                    commandString = "";

                    if (parseState != ParseState.Start && parseState != ParseState.ReadingQuantifier)
                    {
                        if (k == 'g')
                        {
                            command = new Command { Mode = state, Direction = Direct.Up, Quantity = 0 };
                            validity = EntireCommandStatus.Valid;
                            return validity;
                        }

                        return NotValid();
                    }

                    switch (k)
                    {
                        case '_':
                            FoundTheSearchedVerse();
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case '|':
                            validity = EntireCommandStatus.Incomplete;
                            return validity;

                        case '<':
                            command = new Command { Mode = state, Direction = Direct.Down, Quantity = 20 };
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case '>':
                            command = new Command { Mode = state, Direction = Direct.Up, Quantity = 20 };
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case 'g':
                            parseState = ParseState.FilterBookAlias;
                            commandString += k;
                            validity = EntireCommandStatus.Incomplete;
                            return validity;

                        case 'G':
                            command = new Command { Mode = state, Direction = Direct.Down, Quantity = 0 };
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case 'i':
                            normalChoosingMode = false;
                            commandString = choiceCommandBuffer;
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case 'c':
                            normalChoosingMode = false;
                            choiceCommandBuffer = "";
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        default:
                            return FeedMovement(k);
                    }
                }
                else
                {
                    
                    //filtering out choices mode
                    switch (k)
                    {
                        case '|':
                            normalChoosingMode = true;
                            validity = EntireCommandStatus.Valid;
                            return validity;
                        case '<':
                        case '>':
                            break;
                        case '{':
                            if (choiceCommandBuffer != "")
                            {
                                choiceCommandBuffer = choiceCommandBuffer[..^1];
                            }
                            break;
                        default:
                            choiceCommandBuffer += k;
                            break;
                    }
                    return ShipOutChoiceCommand(choiceCommandBuffer);
                }
            }
            else
            {
                activeFilter = null;
                return NotValid();
            }
        }

        private EntireCommandStatus FinishFilterQuantifier(char k, string whichFilter)
        {
            {
                if (quantifier == 0 && k == ' ')
                {
                    AddFilter(activeFilter.ToString(), " ", true);
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }
                //user (may have) typed an alias to a number
                if (Quantifiers.TryGetValue(k, out int digit))
                {
                    quantifier = (quantifier) * 10 + digit;
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }

                //user typed an actual number
                if (char.IsDigit(k))
                {
                    quantifier = (quantifier) * 10 + (int)char.GetNumericValue(k);
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }

                //must be a movement-terminated command
                //if (quantifier == 0) { quantifier = 1; }

                if (k == 'j' || k == 'k' || k == ' ')
                {
                    string quantifierString = quantifier.ToString();
                    string optionString = quantifierString + k.ToString();

                    AddFilter(whichFilter, optionString, false); 
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }
                else
                {
                    activeFilter = null;
                    return NotValid();
                }
            }
        }

        private void AddFilter(string whichFilter, string option, bool weAreDeleting)
        {
            string[] filterParts = wholeQueryWithCommas.Split(',');
            string newFilter = whichFilter + option;
            int filterIndex;
            switch (whichFilter)
            {
                case "b":
                    filterIndex = 1;
                    break;
                case "c":
                    filterIndex = 2;
                    break;
                case "v":
                    filterIndex = 3;
                    break;
                case "t":
                    filterIndex = 4;
                    break;
                case "l":
                    filterIndex = 5;
                    break;
                case "o":
                    filterIndex = 6;
                    break;
                case "m":
                    filterIndex = 7;
                    break;
                default:
                    filterIndex = 0;
                    break;
            }
            if (weAreDeleting)
            {
                filterParts[filterIndex] = "";
            }
            else
            {
                filterParts[filterIndex] = newFilter;
            }
            activeFilter = null;
            wholeQueryWithCommas = string.Join(',', filterParts);
            AFilterWasChanged();
        }

        private EntireCommandStatus HistoryCommands(char k)
        {
            if (k == 'r')
            {
                command = new Command { Mode = state, Quantity = 0, Direction = Direct.Up };
                validity = EntireCommandStatus.Valid;
                return validity;
            }
            if (k == 'z')
            {
                command = new Command { Mode = state, Quantity = 0, Direction = Direct.Down };
                validity = EntireCommandStatus.Valid;
                return validity;
            }
            validity = EntireCommandStatus.Invalid;
            return validity;
        }

        private EntireCommandStatus FeedMovement(char k)
        {
            //user (may have) typed an alias to a number
            if (Quantifiers.TryGetValue(k, out int digit)) 
            {
                quantifier = (quantifier) * 10 + digit;
                parseState = ParseState.ReadingQuantifier;
                validity = EntireCommandStatus.Incomplete;
                return validity;
            }

            //user typed an actual number
            if (char.IsDigit(k))
            {
                quantifier = (quantifier) * 10 + (int)char.GetNumericValue(k);
                parseState = ParseState.ReadingQuantifier;
                validity = EntireCommandStatus.Incomplete;
                return validity;
            }

            //must be a movement-terminated command
            if (quantifier == 0) { quantifier = 1; }

            if (k == 'j')
            {
                return CompleteMovement(Direct.Down);
            }
            if (k == 'k')
            {
                return CompleteMovement(Direct.Up);
            }

            //shouldn't reach here
            return NotValid();
        }

        private EntireCommandStatus CompleteMovement(Direct direction)
        {
            if (quantifier != 0)
            {
                command = new Command
                {
                    Mode = state,
                    Quantity = quantifier,
                    Direction = direction
                };
                parseState = ParseState.Start;
                quantifier = 0;

                validity = EntireCommandStatus.Valid;
                return validity;
            }

            validity = EntireCommandStatus.Invalid;
            return validity;
        }

        private EntireCommandStatus TranslationChange()
        {
            command = new Command
            {
                Mode = state,
                Quantity = 200,
                Direction = Direct.Absolute
            };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private EntireCommandStatus ToggleUI()
        {
            command = new Command { Mode = state, Quantity = 300, Direction = Direct.Absolute };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private EntireCommandStatus HelpWasRequested()
        {
            command = new Command { Mode = state, Quantity = 400, Direction = Direct.Absolute };
            validity = EntireCommandStatus.Valid;
            return validity;
        }
         
        private EntireCommandStatus CopyTheVerse()
        {
            command = new Command { Mode = state, Quantity = 500, Direction = Direct.Absolute };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private EntireCommandStatus CopyTheTitle()
        {
            command = new Command { Mode = state, Quantity = 600, Direction = Direct.Absolute };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private void AFilterWasChanged()
        {
            command = new Command { Mode = State.Query, Direction = Direct.Absolute, Quantity = 1, SearchText = wholeQueryWithCommas};
        }

        private EntireCommandStatus ShipOutProperSearchCommand()
        {
            command = new Command { Mode = State.Query, Quantity = 0, Direction = Direct.Absolute, SearchText = wholeQueryWithCommas };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private void EnterQueryState()
        {
            SearchUiVisibilityChanged?.Invoke(true);  // Open search UI
            commandString = "/" + searchQuery;
        }

        private void FoundTheSearchedVerse()
        {
            command = new Command { Mode = State.Choice, Direction = Direct.Absolute, Quantity = 0 };
        }

        private EntireCommandStatus ShipOutChoiceCommand(string  searchText)
        {
            commandString = searchText;
            command = new Command { Mode = state, Direction = Direct.Absolute, Quantity = 1, SearchText = searchText };
            validity = EntireCommandStatus.Valid;
            return validity;
        }
    }
}
