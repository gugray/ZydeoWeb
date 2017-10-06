import matplotlib.pyplot as plt
import numpy as np
import string
import csv
import sys
import math
import re

csv.field_size_limit(sys.maxsize)

bucket_count = 99
words = {}

matcher = re.compile("[^ ]+ ([^ ]+)[^\]]+\] (.+)")
with open("../_work_align/cedict_ts.u8", "r", encoding="utf8") as f:
  for line in f:
    if (line.startswith("#")): continue
    if (len(line) == 0): continue
    m = matcher.match(line)
    if m == None: continue
    word = m.group(1)
    senses = m.group(2)
    slashcount = senses.count('/')
    semiccont = senses.count(';')
    if word not in words: words[word] = 0
    sensecount = slashcount - 1
    words[word] += sensecount

wdtorank_colloc = {}
wdtorank_jieba = {}
wdtorank_subtlex = {}

with open("../_work_align/04-zh-wordfreqs-colloc.txt", "r", encoding="utf8") as f:
    reader = csv.reader(f, delimiter='\t')
    rank = 0
    for inrow in reader:
      wdtorank_colloc[inrow[1]] = rank
      rank += 1
with open("../_work_align/04-zh-wordfreqs-jieba.txt", "r", encoding="utf8") as f:
    reader = csv.reader(f, delimiter='\t')
    rank = 0
    for inrow in reader:
      wdtorank_jieba[inrow[1]] = rank
      rank += 1
with open("../_work_align/subtlex-ch.txt", "r", encoding="utf8") as f:
    reader = csv.reader(f, delimiter='\t')
    rank = 0
    for inrow in reader:
      wdtorank_subtlex[inrow[0]] = rank
      rank += 1

seen_colloc = 0
seen_jieba = 0
seen_subtlex = 0
for word in words:
  if word in wdtorank_colloc: seen_colloc += 1
  if word in wdtorank_jieba: seen_jieba += 1
  if word in wdtorank_subtlex: seen_subtlex += 1

buckets_colloc = []
buckets_jieba = []
buckets_subtlex = []

for i in range(0, bucket_count):
    bucket = { 'ix': i, 'words': 0, 'senses': 0 }
    buckets_colloc.append(bucket)
    bucket = { 'ix': i, 'words': 0, 'senses': 0 }
    buckets_jieba.append(bucket)
    bucket = { 'ix': i, 'words': 0, 'senses': 0 }
    buckets_subtlex.append(bucket)

for word in words:
  if word in wdtorank_colloc:
    rank = wdtorank_colloc[word]
    buckix = int(math.floor(rank / 1000))
    if buckix < bucket_count:
      buckets_colloc[buckix]['words'] += 1
  if word in wdtorank_jieba:
    rank = wdtorank_jieba[word]
    buckix = int(math.floor(rank / 1000))
    if buckix < bucket_count:
      buckets_jieba[buckix]['words'] += 1
  if word in wdtorank_subtlex:
    rank = wdtorank_subtlex[word]
    buckix = int(math.floor(rank / 1000))
    if buckix < bucket_count:
      buckets_subtlex[buckix]['words'] += 1

xarr = np.arange(len(buckets_colloc))
yarr1 = []
yarr2 = []
yarr3 = []
for i in xarr:
  words = buckets_colloc[i]['words']
  yarr1.append(words)
  words = buckets_jieba[i]['words']
  yarr2.append(words)
  words = buckets_subtlex[i]['words']
  yarr3.append(words)
plt.plot(xarr, yarr1, color='blue', label='colloc')
plt.plot(xarr, yarr2, color='red', label='jieba')
plt.plot(xarr, yarr3, color='green', label='subtlex')
plt.ylim([0,1000])
plt.legend()
plt.show()