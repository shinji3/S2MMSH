S2MMSH
======

なんでも実況V　ストリーミング変換サーバ

##STORM (Stream to relayable MMSH)に変わる模様  
※reliableではない可能性が高い 

[![Bitdeli Badge](https://d2weczhvl823v0.cloudfront.net/kikakubu-ksg/s2mmsh/trend.png)](https://bitdeli.com/free "Bitdeli Badge")

##Update
2013/10/08 ffmpegのコマンドを直修できるようにした。  
2013/10/08 ffmpegのエラーをログに出力するようにした。  

##What is this  
ffmpegで認識可能なストリーミングデータについて、何実でライブ配信可能なmmsh形式に変換するツール  
入力元はFFMPEGランチャーやMMSストリームなど。  
注）現バージョンはKagamin2最新バージョンからのアクセスのみサポートしています。  

##How to use  
[入力にFFMPEGランチャーを使う場合]  
FFMPEGランチャーの出力にはtcp://IP:ポート?listenを使用。  
S2MMSHの入力ストリームにtcp://IP:ポートを指定し、平均ビットレートにはFFMPEGランチャーで指定した値を使用（おまじない）  

[入力にMMSソースを使う場合]  
プロトコルはmmsh://に書き換える。 （http://とmms://は自動的に置き換えられる。）

[その他のソースを使う場合]  
RTSP、RTMP等、ffmpegが対応しているプロトコルについては変換可能。youtubeとかニコ生とかはURLさえ分かればだいたい変換可能？  
元ストリームのエンコード形式にかかわらずASFコンテナに突っ込むため、プレイヤーによって再生可否が大きく変動  
再生されやすさは、ffplay >>>>> VLC >>>>> GOM >>>WMPくらい  
再変換を行った場合はほぼ再生可能。  

[TIPS：ustreamをソースに使う場合]  
直修モードでこんな感じ。なんでこれでいけるのかは謎ですが（処理はrtmpdumpと同様っぽい）  
 -v error -i "rtmp://flash10.ustream.tv/ustreamVideo/11865965/ playpath=streams/live swfUrl=http://static-cdn1.ustream.tv/swf/live/viewer.rsl:96.swf swfVfy=1 live=1" -c copy -f asf_stream -  
「rtmp://flash10.ustream.tv/ustreamVideo/11865965/」の部分だけamfファイルから拾ってくる。  
・簡単な説明。詳細はググれ  
１．ustreamの配信urlのソースからcidを拾う  
２．http://cdngw.ustream.tv/Viewer/getStream/1/cid（←ここに入れる）.amfのファイルを取得  
３．amfファイルの中に「rtmp://flashXX.ustream.tv/ustreamVideo/（cid）」みたいなのがある  
※ケツにスラッシュいれないとダメぽいです。  
※ていうかユーストはこんなめんどいことしなくてもGOM用アドレスあったよなそういや  


##Issues  
各鏡ツールとプレイヤーへの対応。（クライアントによって投げてくるhttpヘッダがまちまちなのよ）  
RTMPサーバ化  
$C（Change Notification）対応  ⇒鏡が対応してないので無理  
バックグラウンドストリーム（キャンバスストリーム）対応  
プレイリスト対応  
音声ストリーム追加（副音声追加）対応  
ピアキャスプロトコル対応  
統合エンコーダツールとして展開  
  
