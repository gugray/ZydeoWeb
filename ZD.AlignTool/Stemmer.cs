using System;
using System.Text;
using System.Collections.Generic;

namespace ZD.AlignTool
{
    public class Stemmer
    {
        public enum Flags
        {
            STEM, PREFIX, COMP_MEMBER, COMP_DELIM,
            COMP_MUST_HAVE, COMP_BEFORE_HYPHEN, STEM_IF_COMP, INT_PUNCT
        }

        protected Dictionary<string, HashSet<Flags>> tag_config = new Dictionary<string, HashSet<Flags>>();
        protected Dictionary<string, string> tag_convert = new Dictionary<string, string>();
        protected Dictionary<string, string> tag_replace = new Dictionary<string, string>();
        protected string copy2surface;

        public class MorphemeInfo
        {
            public string lexical = "", surface = "", category = "";
            public bool isPrefix, isStem, isDerivative, isCompoundMember, isCompoundDelimiter;
            public HashSet<Flags> flags, flags_conv;
        }

        public class Stem
        {
            public List<MorphemeInfo> morphs = new List<MorphemeInfo>();

            public string szAccentedForm = "", szStem = "";
            public int iStemCode = -1;
            public int nCompounds = 0;
            public bool bCompoundWord = false;
            public bool bIncorrectWord = false;
            public List<int> compoundDelims = new List<int>();

            public Stem()
            {
            }

            public string getTags(bool all)
            {
                string res = "";
                for (int n = 0; n < morphs.Count; ++n)
                {
                    MorphemeInfo m = morphs[n];
                    if (!all && n < iStemCode && !m.isPrefix) continue;
                    res += "[" + m.category + "]";
                }

                return res;
            }
        }

        public Stemmer()
        {
            // From https://github.com/dlt-rilmta/hunlp-GATE/blob/master/Lang_Hungarian/resources/hfst/hfst-wrapper.props
            Dictionary<string, string> props = new Dictionary<string, string>();
            props["STEM"] = "/Adj;/Adj|Abbr;/Adj|Attr;/Adj|Attr|Abbr;/Adj|Attr|Pro;/Adj|Attr|Pro|Rel;/Adj|FN;/Adj|Pred;/Adj|Pred|Pro;/Adj|Pro;/Adj|Pro|Int;/Adj|Pro|Rel;/Adj|Unit;/Adj|col;/Adj|nat;/Adv;/Adv|(Adj);/Adv|(Num);/Adv|Abbr;/Adv|Acronx;/Adv|AdjMod;/Adv|Pro;/Adv|Pro|Abbr;/Adv|Pro|Int;/Adv|Pro|Rel;/CmpdPfx;/Cnj;/Cnj|Abbr;/Det;/Det|Pro;/Det|Pro|(Post);/Det|Pro|Int;/Det|Pro|Rel;/Det|Pro|def;/Det|Q;/Det|Q|indef;/Det|art.Def;/Det|art.NDef;/Ger:At;/Inj-Utt;/N;/Num;/Num|Abbr;/Num|Attr;/Num|Digit;/Num|Pro;/Num|Pro|Int;/Num|Pro|Rel;/Num|Roman;/N|Abbr;/N|Abbr|ChemSym;/N|Abbr|Unit;/N|Acron;/N|Acronx;/N|Ltr;/N|Pro;/N|Pro|(Post);/N|Pro|Abbr;/N|Pro|Int;/N|Pro|Rel;/N|Unit;/N|Unit|Abbr;/N|def;/N|lat;/N|mat;/Post;/Post|(Abl);/Post|(All);/Post|(Ela);/Post|(Ins);/Post|(N0);/Post|(Poss);/Post|(Subl);/Post|(Supe);/Post|(Ter);/Prep;/Prev;/QPtcl;/Slash;/Space;/S|Abbr;/S|Acron;/V;/V|Abbr;/X;/X|Abbr;Hyph:Dash;Hyph:Hyph;Hyph:Slash;_Abe/Adj;_AdjVbz_Ntr/V;_AdjVbz_Tr/V;_Adjz:i/Adj;_Adjz:s/Adj;_Adjz:Ó/Adj;_Adjz:Ú/Adj;_Adjz_Hab/Adj;_Adjz_Loc:beli/Adj;_Adjz_Ord:VdlAgOs/Adj;_Adjz_Quant/Adj;_Adjz_Type:fajta/Adj;_Adjz_Type:féle/Adj;_Adjz_Type:szerű/Adj;_AdvPtcp:ttOn/Adv;_AdvPtcp:vÁst/Adv;_Advz:lAg/Adv;_Advz:rét/Adv;_Advz_LocDistr:szerte/Adv;_Advz_Quant:szám/Adv;_Des/N;_Dim:cskA/Adj;_Dim:cskA/N;_EssFor:kéntMA/Adj;_FutPtcp/Adj;_Ger:tA/N;_Ger/N;_ImpfPtcp/Adj;_MedPass/V;_MltComp/Adv;_ModPtcp/Adj;_Mrs/N;_NAdvz:ilAg/Adv;_VAdjz%:nivaló/Adj;_NVbz:l/V;_NVbz_Ntr:zik/V;_NVbz_Tr:z/V;_NegModPtcp/Adj;_NegPtcp/Adj;_Nz:s/N;_Nz_Abstr/N;_Nz_Type:féleség/N;_Nz_Type:szerűség/N;_Pass/V;_PerfPtcp/Adj;_PerfPtcp_Subj=tA/Adj;_VAdvz:ÓlAg/Adv;_VNz:nivaló/N;_Vbz:kOd/V";
            props["PREFIX"] = "/Supl";
            props["COMP_MEMBER"] = "/N;/Adj;/V;/CmpdPfx;/N|Acron;/N|Acronx;/N|Abbr;/N|lat|Abbr;/N|lat;/N|Unit;/N|mat";
            props["COMP_DELIM"] = "/Prev";
            props["COMP_MUST_HAVE"] = "/N;/Adj;/N|Acron;/N|Acronx;/N|Abbr;/N|lat|Abbr;/N|lat;/N|Unit;/N|mat";
            props["COMP_BEFORE_HYPHEN"] = "Nom;/N;/Adj;/N|Acron;/N|Acronx;/N|Abbr;/N|lat|Abbr;/N|lat;/N|Unit;/N|mat;/Num;/Num|Attr";
            props["STEM_IF_COMP"] = "_ImpfPtcp/Adj;_Adjz:s/Adj;_Adjz:Ó/Adj;_Ger/N";
            props["INT_PUNCT"] = "Hyph:Dash;Hyph:Hyph;Hyph:Slash";
            string stemmerConvert = "_Abe/Adj=/Adj;_AdjVbz_Ntr/V=/V;_AdjVbz_Tr/V=/V;_Adjz:i/Adj=/Adj;_Adjz:s/Adj=/Adj;_Adjz:Ó/Adj=/Adj;_Adjz:Ú/Adj=/Adj;_Adjz_Hab/Adj=/Adj;_Adjz_Loc:beli/Adj=/Adj;_Adjz_Ord:VdlAgOs/Adj=/Adj;_Adjz_Quant/Adj=/Adj;_Adjz_Type:fajta/Adj=/Adj;_Adjz_Type:féle/Adj=/Adj;_Adjz_Type:szerű/Adj=/Adj;_AdvPerfPtcp/Adv=/Adv;_AdvPtcp:ttOn/Adv=/Adv;_AdvPtcp:vÁst/Adv=/Adv;_AdvPtcp/Adv=/Adv;_Advz:lAg/Adv=/Adv;_Advz:rét/Adv=/Adv;_Advz_LocDistr:szerte/Adv=/Adv;_Advz_Quant:szám/Adv=/Adv;_Aggreg/Adv=/Adv;_Caus/V=/V;_Com:stUl/Adv=/Adv;_Comp/Adj=/Adj;_Comp/Adv=/Adv;_Comp/Adv|Pro=/Adv|Pro;_Comp/Num=/Num;_Comp/N|Pro=/N|Pro;_Comp/Post|(Abl)=/Post|(Abl);_Comp/Post|(All)=/Post|(All);_Des/N=/N;_Design/Adj=/Adj;_Dim:cskA/Adj=/Adj;_Dim:cskA/N=/N;_Distr:nként/Adv=/Adv;_DistrFrq:ntA/Adv=/Adv;_EssFor:kéntMA/Adj=/Adj;_Frac/Num=/Num;_Freq/V=/V;_FutPtcp/Adj=/Adj;_Ger:tA/N=/N;_Ger/N=/N;_ImpfPtcp/Adj=/Adj;_Manner:0/Adv=/Adv;_Manner/Adv=/Adv;_MedPass/V=/V;_Mlt-Iter/Adv=/Adv;_MltComp/Adv=/Adv;_Mod/V=/V;_ModPtcp/Adj=/Adj;_Mrs/N=/N;_NAdvz:ilAg/Adv=/Adv;_VAdjz%:nivaló/Adj=/Adj;_NVbz:l/V=/V;_NVbz_Ntr:zik/V=/V;_NVbz_Tr:z/V=/V;_NegModPtcp/Adj=/Adj;_NegPtcp/Adj=/Adj;_Nz:s/N=/N;_Nz_Abstr/N=/N;_Nz_Type:féleség/N=/N;_Nz_Type:szerűség/N=/N;_Ord/Adj=/Adj;_OrdDate/N=/N;_Pass/V=/V;_PerfPtcp/Adj=/Adj;_PerfPtcp_Subj=tA/Adj /Adj;_Supe/N=/N;_Tmp_Ante/Adv=/Adv;_Tmp_Loc/Adv=/Adv;_VAdvz:ÓlAg/Adv=/Adv;_VNz:nivaló/N=/N;_Vbz:kOd/V=/V";
            string stemmerReplace = "/Adj|col=/Adj;/Adj|nat=/Adj;/N|mat=/N;/N|Acron=/N;/N|Unit=/N;/N|Unit|Abbr=/N;/N|Abbr|ChemSym=/N;/N|Ltr=/N;/N|Acronx=/N;/N|Abbr=/N;/N|def=/N;/N|lat|Abbr=/N;/N|lat=/N;/Adj|lat=/Adj;/Num|lat=/Num";

            //string item_sep = props.getProperty("stemmer.item_sep", ";");
            //string value_sep = props.getProperty("stemmer.value_sep", "=");
            foreach (object o in Enum.GetValues(typeof(Flags)))
            {
                string f = o.ToString();
                string[] vals = props[f].Split(';');
                foreach (string t in vals)
                {
                    HashSet<Flags> flags;
                    if (!tag_config.ContainsKey(t))
                    {
                        flags = new HashSet<Flags>();
                        tag_config[t] = flags;
                    }
                    else flags = tag_config[t];
                    flags.Add((Flags)Enum.Parse(typeof(Flags), f));
                }
            }

            string[] parts = stemmerConvert.Split(';');
            foreach (string p in parts)
            {
                string[] opt = p.Split('=');
                if (opt.Length < 2) continue;
                tag_convert[opt[0]] = opt[1];
            }


            parts = stemmerReplace.Split(';');
            foreach (string p in parts)
            {
                string[] opt = p.Split('=');
                if (opt.Length < 2) continue;
                tag_replace[opt[0]] = opt[1];
            }

            copy2surface = "* *";
        }

        // rough port from c++
        public Stem process(string input)
        {
            Stem stem = new Stem();
            int iState = 0;

            bool iItIsStem = false;
            bool bDerivative = false;
            bool bCompoundMember = false;
            int nMustHaveCompounds = 0;         //how many morphemes with "compound must have" property
            int nLastStemCode = -1;     //last stem position
            int nPrevLastStemCode = -1; //prev state of nLastStemCode
            int iHyphenPos = -1;   //position of a hyphen
            bool bLookForCompound = false;

            bool bSurfLexDiff = false;
            bool sureCompound = false;
            bool prevCompound = false;

            string szCurCod = "";
            string surface = ""; //lexical prev_lexical, prev_surface; 

            MorphemeInfo morph = new MorphemeInfo();

            foreach (char c in input)
            {
                switch (iState)
                {
                    case 0:
                        if (c == '[')
                        {
                            iState = 1;
                            break;
                        }
                        if (c == '=')
                        {
                            iState = 2;
                            bSurfLexDiff = true;
                            break;
                        }
                        if (c == '+')
                        {
                            bSurfLexDiff = false;
                            break; //ignoring '+' in lexical form
                        }
                        stem.szAccentedForm += c;
                        morph.lexical += c;
                        break;
                    case 1:
                        if (c == ']')
                        {
                            morph.flags = null;
                            if (tag_config.ContainsKey(szCurCod)) morph.flags = tag_config[szCurCod];
                            if (morph.flags == null) morph.flags = new HashSet<Flags>();

                            iItIsStem = morph.flags.Contains(Flags.STEM);
                            bCompoundMember = morph.flags.Contains(Flags.COMP_MEMBER);

                            //conversion
                            string tagc = null;
                            if (tag_convert.ContainsKey(szCurCod)) tagc = tag_convert[szCurCod];
                            bDerivative = tagc != null;
                            morph.flags_conv = null;
                            if (bDerivative && tag_config.ContainsKey(tagc)) morph.flags_conv = tag_config[tagc];
                            if (morph.flags_conv == null) morph.flags_conv = new HashSet<Flags>();

                            //tag replacement
                            string r = null;
                            if (tag_replace.ContainsKey(szCurCod)) r = tag_replace[szCurCod];
                            if (r != null)
                            {
                                szCurCod = r;
                                HashSet<Flags> f2 = null;
                                if (tag_config.ContainsKey(szCurCod)) f2 = tag_config[szCurCod];
                                if (f2 != null) morph.flags = f2;
                            }

                            morph.category = szCurCod;
                            morph.isStem = iItIsStem;
                            morph.isDerivative = bDerivative;
                            morph.isCompoundMember = bCompoundMember;
                            morph.isCompoundDelimiter = morph.flags.Contains(Flags.COMP_DELIM);
                            morph.isPrefix = morph.flags.Contains(Flags.PREFIX);
                            morph.surface = (bSurfLexDiff ? surface : morph.lexical);

                            if (morph.flags.Contains(Flags.COMP_MUST_HAVE) || (morph.flags_conv != null && morph.flags_conv.Contains(Flags.COMP_MUST_HAVE))) nMustHaveCompounds++;

                            stem.morphs.Add(morph);

                            //van-e 2 egymast koveto compound member, (ha igen, tuti osszetett)
                            if (prevCompound && bCompoundMember)
                                sureCompound = true;
                            prevCompound = bCompoundMember;

                            //ha volt mar to es ez kepzo => a konvertaltjait megkeressuk, ha compound member, akkor beallitjuk
                            if (bLookForCompound && morph.flags_conv != null && morph.flags_conv.Contains(Flags.COMP_MEMBER))
                            {
                                bCompoundMember = true;
                                morph.isCompoundMember = true;
                            }

                            if (iItIsStem)
                            {
                                if ("-" == morph.lexical)
                                    iHyphenPos = stem.morphs.Count - 1;
                                if (stem.iStemCode == -1) stem.iStemCode = stem.morphs.Count - 1;//save pos...
                                nLastStemCode = stem.morphs.Count - 1;
                                if (nPrevLastStemCode != -1 && "-" != morph.lexical)
                                {
                                    bool convert = false;
                                    for (int h = nLastStemCode; h >= nPrevLastStemCode; h--)
                                    {
                                        MorphemeInfo m = stem.morphs[h];
                                        if (m.isStem) convert = true;
                                        if (convert && m.isDerivative)
                                        {
                                            string tagc2 = null;
                                            if (tag_convert.ContainsKey(m.category)) tagc2 = tag_convert[m.category];
                                            m.category = tagc2; m.flags = m.flags_conv;
                                            if (m.flags != null && m.flags.Contains(Flags.STEM))
                                            {
                                                m.isStem = true;
                                            }
                                        }
                                    }
                                }
                                nPrevLastStemCode = nLastStemCode;
                                if (!bDerivative)
                                    bLookForCompound = true; //elso toalkoto utan bekapcsoljuk, ha ez true, akkor keresunk olyan kepzot, ami compound membert csinal belole
                            }

                            //ha cmember => novelem
                            //ha to ES jon egy compoundMember kepzo => novelem
                            if (bCompoundMember)
                            {
                                stem.nCompounds++;
                                bLookForCompound = false;
                            }
                            morph = new MorphemeInfo();
                            szCurCod = "";
                            iState = 2;
                            break;
                        }
                        szCurCod += c;
                        if (c == '`') szCurCod = ""; //6-3-as szabaly miatt (2011.07.18. NA: "Azt kene csinalni, hogy a morfologia altal visszaadott cimkek elejen levo reszt a `-ig ki kell torolni mielott bármi mást csinalnal")
                        break;
                    case 2:
                        if (c == '+')
                        {
                            iState = 0;
                            //iLastPlusPos = curr_analysis.szAccentedForm.length();
                        }
                        else if (c == '=')
                            iState = 3;
                        break;
                    case 3:
                        //surface form is arriving, it may replace stem
                        if (c == '+')
                        {
                            iState = 0;
                            MorphemeInfo last = stem.morphs.Count > 0 ? stem.morphs[stem.morphs.Count - 1] : null;

                            if (last != null) surface = Copy2Surface(last.lexical, surface); //copy spec cars from lexical
                                                                                             //if (m_GetCaseFromInput)
                                                                                             //	CaseConvert(surface, (curr_analysis.morp.end()-1)->lexical/*prev_lexical*/); // lexical gets case state from surface
                                                                                             //else 
                            if (stem.nCompounds > 1 && iHyphenPos != stem.morphs.Count - 2/*curr_analysis.bCompoundWord*/)
                                last.lexical = last.lexical.ToLower(); //if it is in compound word: lowercase ("WolfGang"=>"Wolfgang")

                            if (last != null) last.surface = surface;
                            surface = "";
                        }
                        else
                            surface += c;
                        break;
                }

                if (iState == 5)
                {
                    break;
                }
            }

            if (surface != "")
            { // surface form es nincs utana semmi
                MorphemeInfo last = stem.morphs.Count > 0 ? stem.morphs[stem.morphs.Count - 1] : null;

                if (last != null) surface = Copy2Surface(last.lexical, surface); //copy spec cars from lexical
                                                                                 //if (m_GetCaseFromInput)
                                                                                 //	CaseConvert(surface, (curr_analysis.morp.end()-1)->lexical/*prev_lexical*/); // lexical gets case state from surface
                                                                                 //else 
                if (stem.nCompounds > 1 && iHyphenPos != stem.morphs.Count - 2/*curr_analysis.bCompoundWord*/)
                    last.lexical = last.lexical.ToLower(); //if it is in compound word: lowercase ("WolfGang"=>"Wolfgang")

                if (last != null) last.surface = surface;
            }

            // === creating stem ===

            // is it compound?
            /* 
                -ha 2 tove van
                -ha 1 tove + (conv->FN OR stem if compound)


            teszt-esetek:
                nagybefekteto
                husdarabolo
                husdarabologep
                darabolo-evo
                daraboloevo
                darabologep
                Lajos-
                piros-
            */
            //TODO: es ha tobb kotojel van?

            //"tájlátogató-felvilágosító"
            if (sureCompound)
            { //curr_analysis.nCompounds > 1){
              //ez biztos osszetett szo, mert 2 egymast koveto compundmember van benne
              //ha nincs benne FN, de kepzett fonev igen, azt megmenti
              //look for stem if compounds
                for (int n = 0; n < stem.morphs.Count; ++n)
                {
                    MorphemeInfo m = stem.morphs[n];
                    if (m.flags.Contains(Flags.STEM_IF_COMP))
                    {
                        m.isStem = true;
                        m.category = null;
                        if (tag_convert.ContainsKey(m.category)) m.category = tag_convert[m.category];
                        m.flags = m.flags_conv;
                        stem.iStemCode = n;
                        if (n >= nLastStemCode) nLastStemCode = n;
                    }
                }
            }


            bool compound = stem.nCompounds > 1 && (iHyphenPos == -1 || nMustHaveCompounds > 0);
            if (iHyphenPos > 0 && compound)
            {
                //kotojeles akkor lehet osszetett szo, ha a kotojel elott [compound before hyphen] all
                //"aa[FN][NOM]-bb[FN][NOM]" vagy "aa[FN]-bb[FN]" 
                //pl "Arpad-haz"

                MorphemeInfo m = stem.morphs[iHyphenPos - 1];
                if (m.flags.Contains(Flags.COMP_BEFORE_HYPHEN))
                {
                    //ha a kotojel elotti ures es az azt megelozo toalkoto =>
                    if (iHyphenPos > 1 && m.lexical == "" && m.surface == "" && !stem.morphs[iHyphenPos - 2].isStem)
                        compound = false;
                }
                else
                    compound = false; //ha a kotojel elott rag van, akkor ez nem osszetett szo
            }

            stem.bCompoundWord = compound;

            bool internalPunct = false;
            //most megmentjuk attol, hogy a PUNCT, PER vegu szavak to tipusa PUNCT legyen
            for (int n = stem.morphs.Count - 1; n > 0; n--)
            {
                MorphemeInfo m = stem.morphs[n];
                if (!m.flags.Contains(Flags.INT_PUNCT)) break;
                internalPunct = true;
                m.isStem = false;
            }
            while (nLastStemCode > 0 && !stem.morphs[nLastStemCode].isStem)
                nLastStemCode--;

            if (compound && !sureCompound)
            {
                //osszetett szavaknal a stemIfCompoundokat atalakitja
                for (int n = 0; n < stem.morphs.Count; ++n)
                {
                    MorphemeInfo m = stem.morphs[n];
                    if (m.flags.Contains(Flags.STEM_IF_COMP))
                    {
                        m.isStem = true;
                        m.category = null;
                        if (tag_convert.ContainsKey(m.category)) m.category = tag_convert[m.category];
                        m.flags = m.flags_conv;
                        if (n >= nLastStemCode) nLastStemCode = n;
                    }
                }
            }

            //osszetett szavaknal beteszi a + jelet...
            int coffset = 0;
            foreach (MorphemeInfo m in stem.morphs)
            {
                if (m.isCompoundMember || m.isCompoundDelimiter)
                {
                    if (coffset != 0) stem.compoundDelims.Add(coffset); //az utolso nem kell: ott mar vege a szonak
                    coffset += m.surface.Length;
                }
            }


            bool internalPunctAND = true;
            if (internalPunct && iHyphenPos > 0)
            {
                //vegen van egy kotojel, ha elotte ragozoztt szo all, nem lehet szoosszetetel	
                //pl. "magan-" 
                MorphemeInfo m = stem.morphs[iHyphenPos - 1];
                if (m.flags.Contains(Flags.COMP_BEFORE_HYPHEN))
                {
                    //ha a kotojel elotti ures es az azt megelozo toalkoto => 
                    if (iHyphenPos > 1 && m.lexical == "" && m.surface == "" && !stem.morphs[iHyphenPos - 2].isStem)
                    {
                        //hadd eljen, nem megy bele az ikerszo agba
                    }
                    else
                    {
                        internalPunctAND = false; // ez mar ikerszo nem lehet
                    }
                }
            }

            // beleegetjuk hogy a szokozi kotojel stem
            for (int n = 1; n < stem.morphs.Count - 1; n++)
            {
                if (!stem.morphs[n - 1].isStem || !stem.morphs[n + 1].isStem) continue;
                MorphemeInfo m = stem.morphs[n];
                if ("-" == m.surface || "-" == m.lexical)
                {
                    m.isStem = true;
                }
            }

            if (internalPunctAND && iHyphenPos != -1 && !compound)
            {
                //ikerszo

                bool half = false;
                int halfPos = stem.iStemCode;//iHyphenPos;//nLastStemCode;//;
                for (int z = (iHyphenPos > 0 ? iHyphenPos - 1 : 0); z > 0; z--)
                {
                    if (stem.morphs[z].isStem)
                    {
                        halfPos = z;
                        break;
                    }
                }
                string tmp1 = "", tmp2 = "";
                for (int n = 0; n < stem.morphs.Count; n++)
                {
                    MorphemeInfo m = stem.morphs[n];
                    if ("-" == m.lexical)
                    {
                        half = true;
                        halfPos = nLastStemCode;
                    }
                    if (m.isStem)
                    {
                        if (n < halfPos)
                            stem.szStem += m.surface != "" ? m.surface : m.lexical;
                        else
                            stem.szStem += m.lexical;
                    }
                    else
                    {
                        if (!half)
                            tmp1 += m.category + " ";
                        else
                            tmp2 += m.category + " ";
                    }
                }
                if (tmp1 != tmp2)
                {
                    //BAD input, stem is dropped
                    stem.bIncorrectWord = true;
                    // We don't need UNKs
                    //stem.szStem += "UNK";
                    //return 0;
                }


            }
            else
            {
                //simple case

                if (stem.morphs.Count >= nLastStemCode)
                {
                    for (int n = 0; n <= nLastStemCode; n++)
                    {
                        if (!stem.morphs[n].isStem) continue;
                        if (n < nLastStemCode)
                            stem.szStem += stem.morphs[n].surface;
                        else if (n == nLastStemCode/*curr_analysis.iStemCode*/)
                            stem.szStem += stem.morphs[n].lexical;
                    }
                }
            }
            stem.iStemCode = nLastStemCode;

            return stem;
        }

        private string Copy2Surface(string input, string output)
        {
            if (copy2surface == "") return output; //nothing to do :)

            for (int i = 0; i < output.Length; i++)
            {
                if (i >= input.Length) return output;

                if (copy2surface.IndexOf(input[i]) != -1)
                {
                    output = output.Substring(0, i) + input[i] + output.Substring(i);
                }
                else if (input[i] != output[i]) return output;
            }

            return output;
        }

        public string preproc(string ana)
        {
            StringBuilder sb = new StringBuilder();
            string[] parts = ana.Split(' ');
            StringBuilder surf = new StringBuilder();
            StringBuilder lex = new StringBuilder();
            foreach (string part in parts)
            {
                if (part == "") continue;
                string[] sl = part.Split(':');
                if (sl.Length == 3) // :[Hyph:Hyph] and friends
                    sl[1] += ":" + sl[2];
                if (sl.Length > 3) throw new Exception("???");
                if (sl[1].StartsWith("["))
                {
                    if (sb.Length > 0) sb.Append('+');
                    sb.Append(lex);
                    sb.Append(sl[1]);
                    if (surf.Length > 0)
                    {
                        sb.Append('=');
                        sb.Append(surf);
                    }
                    surf.Clear();
                    lex.Clear();
                }
                else
                {
                    surf.Append(sl[0]);
                    lex.Append(sl[1]);
                }
            }
            return sb.ToString();
        }

    }
}
