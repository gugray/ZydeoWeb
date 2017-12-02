import gensim
import io

def write_sims(word, g, sims):
  g.write(word + '\n')
  g.write('-------------------------\n')
  for sim in sims:
    g.write("{0:.4f}".format(sim[1]) + "\t" + sim[0])
    g.write('\n')

def get_write_similarity(model, g, w1, w2):
  val = model.similarity(w1, w2)
  g.write("Similarity: " + w1 + " ~ " + w2 + "\t> " + "{0:.4f}".format(val) + "\n")

def similarities(model, g):
  get_write_similarity(model, g, 'zh_鱼', 'zh_个')
  get_write_similarity(model, g, 'zh_鱼', 'zh_位')
  get_write_similarity(model, g, 'zh_鱼', 'zh_只')
  get_write_similarity(model, g, 'zh_鱼', 'zh_条')
  get_write_similarity(model, g, 'zh_鱼', 'zh_杯')
  get_write_similarity(model, g, 'zh_鱼', 'zh_种')
  get_write_similarity(model, g, 'zh_水', 'zh_个')
  get_write_similarity(model, g, 'zh_水', 'zh_位')
  get_write_similarity(model, g, 'zh_水', 'zh_只')
  get_write_similarity(model, g, 'zh_水', 'zh_条')
  get_write_similarity(model, g, 'zh_水', 'zh_杯')
  get_write_similarity(model, g, 'zh_水', 'zh_种')
  get_write_similarity(model, g, 'zh_污染', 'zh_个')
  get_write_similarity(model, g, 'zh_污染', 'zh_位')
  get_write_similarity(model, g, 'zh_污染', 'zh_只')
  get_write_similarity(model, g, 'zh_污染', 'zh_条')
  get_write_similarity(model, g, 'zh_污染', 'zh_杯')
  get_write_similarity(model, g, 'zh_污染', 'zh_种')
  get_write_similarity(model, g, 'zh_耳朵', 'zh_个')
  get_write_similarity(model, g, 'zh_耳朵', 'zh_双')
  get_write_similarity(model, g, 'zh_耳朵', 'zh_只')
  get_write_similarity(model, g, 'zh_耳朵', 'zh_对')
  get_write_similarity(model, g, 'zh_风', 'zh_个')
  get_write_similarity(model, g, 'zh_风', 'zh_双')
  get_write_similarity(model, g, 'zh_风', 'zh_阵')
  get_write_similarity(model, g, 'zh_风', 'zh_辆')

def multi(model, g):
  sims = model.most_similar(positive=['zh_人', 'zh_火车'], topn=20)

model = gensim.models.Word2Vec.load('../_work_align/zhhu-wv-model.bin')
model.wv.save_word2vec_format('../_work_align/11-wvects.txt')

# with open('../_work_align/_win.txt', encoding='utf-8') as f:
#   with open('../_work_align/_wout.txt', mode='w', encoding='utf-8') as g:
#     #multi(model, g)
#     similarities(model, g)
#     for line in f:
#       word = line.strip()
#       g.write('\n')
#       sims = model.most_similar(positive=[word], topn=20)
#       write_sims(word, g, sims)

#model.most_similar(positive=['woman', 'king'], negative=['man'], topn=1)

