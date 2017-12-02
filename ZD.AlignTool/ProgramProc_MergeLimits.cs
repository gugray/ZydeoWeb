using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ZD.AlignTool
{
    public partial class Program
    {

        static Dictionary<char, HashSet<string>> mergeLimits = new Dictionary<char, HashSet<string>>();
        static HashSet<char> mergeNoEnd = new HashSet<char>();
        static HashSet<char> mergeNoStart = new HashSet<char>();
        static char[] dets = new char[] { '这', '那', '哪', '一' };
        static char[] classifs = new char[] { '把', '包', '杯', '本', '部', '串', '床', '顶', '堵', '对', '份', '封', '副', '个', '根', '罐', '户', '家', '架', '间', '件', '届', '斤', '句', '卷', '棵', '课', '口', '块', '辆', '轮', '匹', '瓶', '起', '群', '首', '双', '艘', '台', '套', '条', '头', '位', '张', '只', '枝', '支', '坐' };

        static bool isMergeBlocked(string a, string b)
        {
            string merged = a + b;
            //if (merged == "我们")
            //    int jfkdsfj = 0;
            if (a.Length == 1)
            {
                if (mergeNoStart.Contains(a[0])) return true;
                if (mergeLimits.ContainsKey(a[0]) && !mergeLimits[a[0]].Contains(merged)) return true;
            }
            if (b.Length == 1)
            {
                if (mergeNoEnd.Contains(b[0])) return true;
                if (mergeLimits.ContainsKey(b[0]) && !mergeLimits[b[0]].Contains(merged)) return true;
            }
            if (Array.IndexOf(dets, a[0]) != -1)
                if (Array.IndexOf(classifs, b[0]) != -1)
                    return true;
            return false;
        }

        static void initMergeLimits()
        {
            mergeNoEnd.Add('不');
            mergeNoEnd.Add('这');
            mergeNoEnd.Add('那');
            mergeNoEnd.Add('哪');
            mergeNoStart.Add('们');
            mergeNoStart.Add('的');

            mergeLimits['吗'] = new HashSet<string>();
            mergeLimits['很'] = new HashSet<string>();
            mergeLimits['我'] = new HashSet<string>();
            mergeLimits['我'].Add("我国");
            mergeLimits['我'].Add("我家");
            mergeLimits['我'].Add("我们");
            mergeLimits['我'].Add("忘我");
            mergeLimits['我'].Add("自我");
            mergeLimits['你'] = new HashSet<string>();
            mergeLimits['你'].Add("你好");
            mergeLimits['你'].Add("你们");
            mergeLimits['你'].Add("迷你");
            mergeLimits['他'] = new HashSet<string>();
            mergeLimits['他'].Add("他们");
            mergeLimits['他'].Add("他人");
            mergeLimits['他'].Add("吉他");
            mergeLimits['他'].Add("其他");
            mergeLimits['她'] = new HashSet<string>();
            mergeLimits['她'].Add("她们");
            mergeLimits['的'] = new HashSet<string>();
            mergeLimits['的'].Add("的话");
            mergeLimits['的'].Add("的确");
            mergeLimits['的'].Add("别的");
            mergeLimits['的'].Add("目的");
            mergeLimits['的'].Add("是的");
            mergeLimits['的'].Add("似的");
            mergeLimits['的'].Add("有的");
            mergeLimits['的'].Add("真的");
            mergeLimits['是'] = new HashSet<string>();
            mergeLimits['是'].Add("是不是");
            mergeLimits['是'].Add("是的");
            mergeLimits['是'].Add("是非");
            mergeLimits['是'].Add("是否");
            mergeLimits['是'].Add("不是");
            mergeLimits['是'].Add("但是");
            mergeLimits['是'].Add("倒是");
            mergeLimits['是'].Add("而是");
            mergeLimits['是'].Add("凡是");
            mergeLimits['是'].Add("还是");
            mergeLimits['是'].Add("或是");
            mergeLimits['是'].Add("即是");
            mergeLimits['是'].Add("就是");
            mergeLimits['是'].Add("可是");
            mergeLimits['是'].Add("老是");
            mergeLimits['是'].Add("算是");
            mergeLimits['是'].Add("要是");
            mergeLimits['是'].Add("于是");
            mergeLimits['是'].Add("真是");
            mergeLimits['是'].Add("只是");
            mergeLimits['是'].Add("总是");
            mergeLimits['了'] = new HashSet<string>();
            mergeLimits['了'].Add("了结");
            mergeLimits['了'].Add("了解");
            mergeLimits['了'].Add("罢了");
            mergeLimits['了'].Add("除了");
            mergeLimits['了'].Add("得了");
            mergeLimits['了'].Add("对了");
            mergeLimits['了'].Add("极了");
            mergeLimits['了'].Add("完了");
            mergeLimits['了'].Add("为了");
        }

        static Regex reAlMerge = new Regex("([a-zA-Z]) ([a-zA-Z])");
        static Regex reNumMerge1 = new Regex("([0-9]) ([0-9])");
        static Regex reNumMerge2 = new Regex("([0-9]) \\. ([0-9])");

        static string mergeAlnum(string ln)
        {
            while (true)
            {
                var m = reAlMerge.Match(ln);
                if (!m.Success) break;
                ln = ln.Replace(m.Value, m.Groups[1].Value + m.Groups[2].Value);
            }
            while (true)
            {
                var m = reNumMerge1.Match(ln);
                if (!m.Success) break;
                ln = ln.Replace(m.Value, m.Groups[1].Value + m.Groups[2].Value);
            }
            while (true)
            {
                var m = reNumMerge2.Match(ln);
                if (!m.Success) break;
                ln = ln.Replace(m.Value, m.Groups[1].Value + "." + m.Groups[2].Value);
            }
            return ln;
        }
    }
}
