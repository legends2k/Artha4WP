using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace WordNet
{
    public class WordNetEngine
    {
        const byte maxPOS = 4;
        const byte posAdjID = 2;
        const char posNoun = 'n', posVerb = 'v', posAdj = 'a', posAdjSat = 's', posAdverb = 'r';
        static string[] pos_name = { "noun", "verb", "adj", "adv", "Oops!" };
        const string strSynonym = "Synonyms";
        const char indexFieldDelim = ' ';
        const char senseFieldDelim = '|';
        static Dictionary<string, StreamReader> _dictFiles = null;
        static Dictionary<char, byte> posCharToIndex = null;
        static Dictionary<string, RelativeFamily> _relatives = null;

        const byte synset_cnt_field = 2;
        const byte ss_type_field = 2;
        const byte word_cnt_field = ss_type_field + 1;
        const byte word_field = word_cnt_field + 1;

        static string[] pointer_symbols = {
                                            " ",
                                            "!",
                                            "@",
                                            "@i",
                                            "~",
                                            "~i",
                                            "#m",
                                            "#s",
                                            "#p",
                                            "%m",
                                            "%s",
                                            "%p",
                                            "=",
                                            "+",
                                            "*",
                                            ">",
                                            ";c",
                                            "-c",
                                            ";r",
                                            "-r",
                                            ";u",
                                            "-u",
                                            "&",
                                            "^"
                                          };
        static string[] relative_names = {
                                            "Synonyms",
                                            "Antonyms",
                                            "Kind of",                  // Hypernym
                                            "Kind of",                  // Instance Hypernyms
                                            "Kinds",                    // Hyponyms
                                            "Kinds",                    // Instance Hyponym
                                            "Member of",                // Member Holonym
                                            "Substance of",             // Substance Holonym
                                            "Part of",                  // Part Holonym
                                            "Members",                  // Member Meronym
                                            "Substances",               // Substance Meronym
                                            "Parts",                    // Part Meronym
                                            "Attributes",
                                            "Derivatives",
                                            "Entails",
                                            "Causes",
                                            "Topics",
                                            "Topical terms",
                                            "Regions",
                                            "Regional terms",
                                            "Usages",
                                            "Usage terms",
                                            "Similar",
                                            "See Also"
                                         };

        public struct Sense
        {
            public string Example
            {
                set;
                get;
            }

            public string Definition
            {
                set;
                get;
            }
        };

        public struct POS : IComparable<POS>
        {
            public byte posType;
            public List<long> offsets;
            public byte tagSenseCount;

            public string POSName
            {
                get
                {
                    return pos_name[posType];
                }
            }

            public List<Sense> Senses
            {
                get;
                set;
            }

            public int CompareTo(POS that)
            {
                // that to this and not this to that;
                // remember we want descending sort and not ascending sort
                int comp = that.tagSenseCount.CompareTo(this.tagSenseCount);
                if (0 == comp)
                {
                    comp = that.offsets.Count.CompareTo(this.offsets.Count);
                }
                return comp;
            }
        };

        public struct RelativeFamily : IComparable<RelativeFamily>
        {
            public byte relType;
            public List<string> Terms
            {
                get;
                set;
            }

            public string RelativeFamilyName
            {
                get
                {
                    return relative_names[relType];
                }
            }

            public int CompareTo(RelativeFamily that)
            {
                return relType.CompareTo(that.relType);
            }
        };

        public WordNetEngine(Dictionary<string, Stream> dictFiles)
        {
            _dictFiles = new Dictionary<string, StreamReader>();
            foreach(KeyValuePair<string, Stream> s in dictFiles)
                _dictFiles.Add(s.Key, new StreamReader(s.Value));

            posCharToIndex = new Dictionary<char, byte>();
            posCharToIndex.Add(posNoun, 0);
            posCharToIndex.Add(posVerb, 1);
            posCharToIndex.Add(posAdj, 2);
            posCharToIndex.Add(posAdjSat, 2);
            posCharToIndex.Add(posAdverb, 3);

            _relatives = new Dictionary<string, RelativeFamily>();
        }

        string getIndexFile(byte pos)
        {
            return string.Concat("index.", pos_name[pos]);
        }

        string getDataFile(byte pos)
        {
            return string.Concat("data.", pos_name[pos]);
        }

        string getDataFile(char pos)
        {
            return getDataFile(posCharToIndex[pos]);
        }

        // pass only neutralised strings to lookup i.e. all spaces should be '_'s
        public List<POS> Lookup(string searchstr, out List<RelativeFamily> relatives)
        {
            List<POS> results = new List<POS>(maxPOS);
            bool retryWithHyphen = false;
            do
            {
                for (byte i = 0; i < maxPOS; ++i)
                {
                    byte tagCount;
                    List<long> _offsets = bin_search(searchstr, i, out tagCount);
                    if (_offsets != null)
                        results.Add(new POS { posType = i, offsets = _offsets, tagSenseCount = tagCount });
                }
                if ((0 == results.Count) && !retryWithHyphen)
                {
                    int firstUnderscoreIndex = searchstr.IndexOf('_');
                    if (firstUnderscoreIndex != -1)
                    {
                        char [] searchChrs = searchstr.ToCharArray();
                        searchChrs[firstUnderscoreIndex] = '-';

                        searchstr = new string(searchChrs);
                        retryWithHyphen = true;
                    }
                }
                else
                    retryWithHyphen = false;
            } while (retryWithHyphen);

            results.Sort();
            _relatives.Clear();

            for (int i = 0; i < results.Count; ++i)
            {
                POS s = results[i];
                s.Senses = fetch_senses(s, searchstr);
                results[i] = s;
            }

            relatives = new List<RelativeFamily>();
            foreach (KeyValuePair<string, RelativeFamily> kv in _relatives)
            {
                List<string> rels = remove_duplicates(kv.Value.Terms);
                relatives.Add(new RelativeFamily { relType = kv.Value.relType, Terms = rels });
            }

            relatives.Sort();

            return results;
        }

        List<string> remove_duplicates(List<string> relatives)
        {
            List<string> relativesSet = new List<string>();
            foreach (string s in relatives)
            {
                if (-1 == relativesSet.IndexOf(s))
                {
                    relativesSet.Add(s);
                }
            }
            return relativesSet;
        }

        // this not only fetches senses, it processes relatives too
        List<Sense> fetch_senses(POS s, string searchstr)
        {
            StreamReader reader = _dictFiles[getDataFile(s.posType)];
            List<Sense> results = new List<Sense>(s.offsets.Count);

            foreach(long offset in s.offsets)
            {
                reader.DiscardBufferedData();
                reader.BaseStream.Position = offset;

                string ln = reader.ReadLine();

                // get the sense first
                int sense_start = ln.LastIndexOf(senseFieldDelim);
                Debug.Assert(sense_start != -1);
                string senseAndExample = ln.Substring(sense_start + 2).TrimEnd().Replace('_', ' ');
                int delim = senseAndExample.IndexOf('"');
                if (delim != -1)
                    results.Add(new Sense { Definition = senseAndExample.Substring(0, delim),
                                            Example = senseAndExample.Substring(delim) });
                else
                    results.Add(new Sense { Definition = senseAndExample });

                // then look up the relatives
                parse_data_line(ln.Substring(0, sense_start), searchstr, s.posType);
            }

            return results;
        }

        struct Relative
        {
            public byte relativeType;
            public long offset;
            public char pos;
        };

        // parses relatives including synonyms
        void parse_data_line(string ln, string searchstr, byte pos)
        {
            string [] fields = ln.Split();
            //char ss_type = fields[ss_type_field][0];

            UInt16 j;
            List<string> synonyms = get_synonms(fields, searchstr, out j);
            if (synonyms.Count > 0)
            {
                trim_adj_marker(ref synonyms, pos);
                try
                {
                    RelativeFamily syns = _relatives[strSynonym];
                    syns.Terms.AddRange(synonyms);
                }
                catch (KeyNotFoundException)
                {
                                                                   // relative type is 0 for synonyms
                    _relatives.Add(strSynonym, new RelativeFamily{ relType = 0, Terms = synonyms });
                }
            }

            UInt16 ptr_cnt = UInt16.Parse(fields[j++]);
            List<Relative> relatives = new List<Relative>();
            for(UInt16 i = 0; i < ptr_cnt; ++i, j += 4)
            {
                sbyte ptrType = (sbyte) Array.IndexOf<string>(pointer_symbols, fields[j]);
                if (-1 != ptrType)
                {
                    long _offset = long.Parse(fields[j + 1]);
                    Relative r = new Relative { relativeType = (byte)ptrType, offset = _offset, pos = fields[j + 2][0] };
                    relatives.Add(r);
                }
            }

            foreach (Relative r in relatives)
            {
                StreamReader reader = _dictFiles[getDataFile(r.pos)];
                reader.DiscardBufferedData();
                reader.BaseStream.Position = r.offset;
                string sln = reader.ReadLine();

                UInt16 temp;
                List<string> rel_terms = get_synonms(sln.TrimEnd().Split(), searchstr, out temp);
                if (rel_terms.Count > 0)
                {
                    trim_adj_marker(ref rel_terms, posCharToIndex[r.pos]);
                    try
                    {
                        RelativeFamily d = _relatives[relative_names[r.relativeType]];
                        d.Terms.AddRange(rel_terms);
                    }
                    catch (KeyNotFoundException)
                    {
                        _relatives.Add(relative_names[r.relativeType], new RelativeFamily { relType = r.relativeType,
                                                                                            Terms = rel_terms });
                    }
                }
            }
        }

        List<string> get_synonms(string [] fields, string exception, out UInt16 lastFieldIndex)
        {
            byte word_cnt = (byte)hex_to_dec(fields[word_cnt_field]);
            List<string> synonyms = new List<string>(word_cnt);
            UInt16 j = word_field;
            for (byte i = 0; i < word_cnt; ++i, j += 2)
            {
                // if synonym's not the same as the searched string, enlist it
                if (0 != fields[j].ToLowerInvariant().CompareTo(exception))
                    synonyms.Add(fields[j].Replace('_', ' '));
            }
            lastFieldIndex = j;
            return synonyms;
        }

        void trim_adj_marker(ref List<string> lemmas, byte pos)
        {
            if (posAdjID == pos)
            {
                for (int i = 0; i < lemmas.Count; ++i)
                {
                    int ofst = lemmas[i].IndexOf('(');
                    if (-1 != ofst)
                    {
                        string s = lemmas[i].Substring(0, ofst);
                        lemmas[i] = s;
                    }
                }
            }
        }

        int hex_to_dec(string hexNo)
        {
            int res = 0;
            byte multiplier = 0;
            for (int i = hexNo.Length - 1; i >= 0; --i)
            {
                res += ((byte)System.Uri.FromHex(hexNo[i])) * ((int)Math.Pow(16, multiplier++));
            }
            return res;
        }

        List<long> bin_search(string searchstr, byte nPOS, out byte tagSenseCount)
        {
            StreamReader fReader = _dictFiles[getIndexFile(nPOS)];
            long begin = 0;
            long end = fReader.BaseStream.Length;
            long pivot = (end - begin) / 2, diff = 0;
            string ln;
            int comp = 0, delimPos;

            do
            {
                fReader.DiscardBufferedData();
                fReader.BaseStream.Position = pivot - 1;

                // if we're in the middle of the line, forward it
                if (pivot != 1)
                    fReader.ReadLine();

                // read the actual line to be analysed
                ln = fReader.ReadLine();

                // compare it
                delimPos = ln.IndexOf(indexFieldDelim);
                comp = string.CompareOrdinal(ln.Substring(0, delimPos), searchstr);

                if (comp < 0)
                {
                    begin = pivot;
                    diff = (end - begin) / 2;
                    pivot = begin + diff;
                }
                else if (comp > 0)
                {
                    end = pivot;
                    diff = (end - begin) / 2;
                    pivot = begin + diff;
                }

            } while ((comp != 0) && (diff != 0));

            List<long> results = null;
            tagSenseCount = 0;

            if (0 == comp)
            {
                string [] fields = ln.TrimEnd().Split();
                Int16 numberOfSynets = Int16.Parse(fields[synset_cnt_field]);
                results = new List<long>(numberOfSynets);
                for (int i = fields.Length - numberOfSynets; i < fields.Length; ++i)
                    results.Add(long.Parse(fields[i]));
                tagSenseCount = byte.Parse(fields[fields.Length - numberOfSynets - 1]);
            }

            return results;
        }
    }
}
