using System;
using System.Collections.Generic;
using ConnectApp.Common.Util;
using ConnectApp.Common.Visual;
using ConnectApp.Components;
using ConnectApp.Components.Markdown;
using ConnectApp.Components.PullToRefresh;
using ConnectApp.Main;
using ConnectApp.Models.ActionModel;
using ConnectApp.Models.Model;
using ConnectApp.Models.State;
using ConnectApp.Models.ViewModel;
using ConnectApp.redux.actions;
using Unity.UIWidgets.animation;
using Unity.UIWidgets.async;
using Unity.UIWidgets.foundation;
using Unity.UIWidgets.painting;
using Unity.UIWidgets.Redux;
using Unity.UIWidgets.rendering;
using Unity.UIWidgets.scheduler;
using Unity.UIWidgets.service;
using Unity.UIWidgets.ui;
using Unity.UIWidgets.widgets;
using UnityEngine;
using Avatar = ConnectApp.Components.Avatar;
using Text = Unity.UIWidgets.widgets.Text;
using TextStyle = Unity.UIWidgets.painting.TextStyle;

namespace ConnectApp.screens {
    public class ArticleDetailScreenConnector : StatelessWidget {
        public ArticleDetailScreenConnector(
            string articleId,
            bool isPush,
            Key key = null
        ) : base(key: key) {
            this.articleId = articleId;
            this.isPush = isPush;
        }

        readonly string articleId;
        readonly bool isPush;

        public override Widget build(BuildContext context) {
            return new StoreConnector<AppState, ArticleDetailScreenViewModel>(
                converter: state => {
                    var article = new Article();
                    state.articleState.articleDict.TryGetValue(key: this.articleId, value: out article);
                    var tagIds = article?.tagIds ?? new List<string>();
                    var tagsDict = state.tagState.tagDict;
                    var tags = new List<Tag>();
                    if (tagIds.isNotNullAndEmpty() && tagsDict.isNotNullAndEmpty()) {
                        tagIds.ForEach(id => {
                            tagsDict.TryGetValue(key: id, out var tag);
                            if (tag != null) {
                                tags.Add(item: tag);
                            }
                        });
                    }

                    return new ArticleDetailScreenViewModel {
                        articleId = this.articleId,
                        loginUserId = state.loginState.loginInfo.userId,
                        isLoggedIn = state.loginState.isLoggedIn,
                        articleDetailLoading = state.articleState.articleDetailLoading,
                        articleDict = state.articleState.articleDict,
                        channelMessageList = state.messageState.channelMessageList,
                        channelMessageDict = state.messageState.channelMessageDict,
                        userDict = state.userState.userDict,
                        userLicenseDict = state.userState.userLicenseDict,
                        teamDict = state.teamState.teamDict,
                        tags = tags,
                        followMap = state.followState.followDict.ContainsKey(state.loginState.loginInfo.userId ?? "")
                            ? state.followState.followDict[state.loginState.loginInfo.userId ?? ""]
                            : new Dictionary<string, bool>()
                    };
                },
                builder: (context1, viewModel, dispatcher) => {
                    var actionModel = new ArticleDetailScreenActionModel {
                        openUrl = url => OpenUrlUtil.OpenUrl(buildContext: context1, url: url),
                        playVideo = (url, verifyType, limitSeconds) => {
                            Navigator.pushNamed(
                                context: context1,
                                routeName: NavigatorRoutes.VideoPlayer,
                                new VideoPlayerScreenArguments {
                                    url = url,
                                    verifyType = verifyType,
                                    limitSeconds = limitSeconds
                                }
                            );
                        },
                        blockArticleAction = articleId => {
                            dispatcher.dispatch(new BlockArticleAction {articleId = articleId});
                            dispatcher.dispatch(new DeleteArticleHistoryAction {articleId = articleId});
                        },
                        blockUserAction = userId => dispatcher.dispatch(new BlockUserAction {blockUserId = userId}),
                        startFetchArticleDetail = () => dispatcher.dispatch(new StartFetchArticleDetailAction()),
                        fetchArticleDetail = id =>
                            dispatcher.dispatch<Future>(
                                CActions.fetchArticleDetail(articleId: id, isPush: this.isPush)),
                        fetchArticleComments = (channelId, currOldestMessageId) =>
                            dispatcher.dispatch<Future>(
                                CActions.fetchArticleComments(channelId: channelId,
                                    currOldestMessageId: currOldestMessageId)
                            ),
                        likeArticle = (id, count) => {
                            AnalyticsManager.ClickLike("Article", articleId: this.articleId);
                            return dispatcher.dispatch<Future>(CActions.likeArticle(articleId: id, addCount: count));
                        },
                        likeComment = message => {
                            AnalyticsManager.ClickLike("Article_Comment", articleId: this.articleId,
                                commentId: message.id);
                            return dispatcher.dispatch<Future>(CActions.likeComment(message: message));
                        },
                        removeLikeComment = message => {
                            AnalyticsManager.ClickLike("Article_Remove_Comment", articleId: this.articleId,
                                commentId: message.id);
                            return dispatcher.dispatch<Future>(CActions.removeLikeComment(message: message));
                        },
                        sendComment = (channelId, content, nonce, parentMessageId, upperMessageId) => {
                            AnalyticsManager.ClickPublishComment(
                                parentMessageId == null ? "Article" : "Article_Comment", channelId: channelId,
                                commentId: parentMessageId);
                            return dispatcher.dispatch<Future>(
                                CActions.sendComment(articleId: this.articleId, channelId: channelId, content: content,
                                    nonce: nonce, parentMessageId: parentMessageId, upperMessageId: upperMessageId));
                        },
                        startFollowUser = userId =>
                            dispatcher.dispatch(new StartFollowUserAction {followUserId = userId}),
                        followUser = userId =>
                            dispatcher.dispatch<Future>(CActions.fetchFollowUser(followUserId: userId)),
                        startUnFollowUser = userId =>
                            dispatcher.dispatch(new StartUnFollowUserAction {unFollowUserId = userId}),
                        unFollowUser = userId =>
                            dispatcher.dispatch<Future>(CActions.fetchUnFollowUser(unFollowUserId: userId)),
                        startFollowTeam = teamId =>
                            dispatcher.dispatch(new StartFetchFollowTeamAction {followTeamId = teamId}),
                        followTeam = teamId =>
                            dispatcher.dispatch<Future>(CActions.fetchFollowTeam(followTeamId: teamId)),
                        startUnFollowTeam = teamId =>
                            dispatcher.dispatch(new StartFetchUnFollowTeamAction {unFollowTeamId = teamId}),
                        unFollowTeam = teamId =>
                            dispatcher.dispatch<Future>(CActions.fetchUnFollowTeam(unFollowTeamId: teamId)),
                        shareToWechat = (type, title, description, linkUrl, imageUrl, path) =>
                            dispatcher.dispatch<Future>(
                                CActions.shareToWechat(
                                    sheetItemType: type,
                                    title: title,
                                    description: description,
                                    linkUrl: linkUrl,
                                    imageUrl: imageUrl
                                )
                            )
                    };
                    return new ArticleDetailScreen(viewModel: viewModel, actionModel: actionModel);
                }
            );
        }
    }

    class ArticleDetailScreen : StatefulWidget {
        public ArticleDetailScreen(
            ArticleDetailScreenViewModel viewModel = null,
            ArticleDetailScreenActionModel actionModel = null,
            Key key = null
        ) : base(key: key) {
            this.viewModel = viewModel;
            this.actionModel = actionModel;
        }

        public readonly ArticleDetailScreenViewModel viewModel;
        public readonly ArticleDetailScreenActionModel actionModel;

        public override State createState() {
            return new _ArticleDetailScreenState();
        }
    }

    enum _ArticleJumpToCommentState {
        Inactive,
        active
    }

    class _ArticleDetailScreenState : State<ArticleDetailScreen>, TickerProvider, RouteAware {
        const float navBarHeight = 44;
        Article _article = new Article();
        User _user = new User();
        Team _team = new Team();
        bool _isHaveTitle;
        float _titleHeight;
        Animation<RelativeRect> _animation;
        AnimationController _controller;
        RefreshController _refreshController;
        string _loginSubId;
        _ArticleJumpToCommentState _jumpState;
        bool _needRebuildWithCachedCommentPosition;
        bool _isPullUp;
        bool animationIsReady;

        float? _cachedCommentPosition;

        public override void initState() {
            base.initState();
            StatusBarManager.statusBarStyle(false);
            this._refreshController = new RefreshController();
            this._isHaveTitle = false;
            this._titleHeight = 0.0f;
            this._controller = new AnimationController(
                duration: TimeSpan.FromMilliseconds(100),
                vsync: this
            );
            var rectTween = new RelativeRectTween(
                RelativeRect.fromLTRB(0, top: navBarHeight, 0, 0),
                RelativeRect.fromLTRB(0, 0, 0, 0)
            );
            this._animation = rectTween.animate(parent: this._controller);
            SchedulerBinding.instance.addPostFrameCallback(_ => {
                this.widget.actionModel.startFetchArticleDetail();
                var modalRoute = ModalRoute.of(context: this.context);
                modalRoute.animation.addStatusListener(listener: this._animationStatusListener);
            });
            this._loginSubId = EventBus.subscribe(sName: EventBusConstant.login_success, args => {
                this.widget.actionModel.startFetchArticleDetail();
                this.widget.actionModel.fetchArticleDetail(arg: this.widget.viewModel.articleId);
            });
            this._jumpState = _ArticleJumpToCommentState.Inactive;
            this._cachedCommentPosition = null;
            this._needRebuildWithCachedCommentPosition = false;
            this._isPullUp = false;
        }
        
        void _animationStatusListener(AnimationStatus status) {
            if(status == AnimationStatus.completed) {
                this.animationIsReady = true;
                this.setState(() => {});
                this.widget.actionModel.fetchArticleDetail(arg: this.widget.viewModel.articleId);
            }
        }

        public override void didChangeDependencies() {
            base.didChangeDependencies();
            Main.ConnectApp.routeObserver.subscribe(this, (PageRoute) ModalRoute.of(context: this.context));
        }

        public override void dispose() {
            EventBus.unSubscribe(sName: EventBusConstant.login_success, id: this._loginSubId);
            Main.ConnectApp.routeObserver.unsubscribe(this);
            // var modalRoute = ModalRoute.of(context: this.context);
            // modalRoute.animation.removeStatusListener(listener: this._animationStatusListener);
            this._controller.dispose();
            base.dispose();
        }

        public Ticker createTicker(TickerCallback onTick) {
            return new Ticker(onTick: onTick, $"created by {this}");
        }

        void pushToLoginPage() {
            Navigator.pushNamed(
                context: this.context,
                routeName: NavigatorRoutes.Login
            );
        }

        public override Widget build(BuildContext context) {
            this.widget.viewModel.articleDict.TryGetValue(key: this.widget.viewModel.articleId,
                value: out this._article);
            if (!this.animationIsReady || this.widget.viewModel.articleDetailLoading && (this._article == null || !this._article.isNotFirst)) {
                return new Container(
                    color: CColors.White,
                    child: new CustomSafeArea(
                        child: new Column(
                            children: new List<Widget> {
                                this._buildNavigationBar(false),
                                new ArticleDetailLoading()
                            }
                        )
                    )
                );
            }

            if (this._article?.channelId == null) {
                return new Container(
                    color: CColors.White,
                    child: new CustomSafeArea(
                        child: new Column(
                            children: new List<Widget> {
                                this._buildNavigationBar(false),
                                new Flexible(
                                    child: new BlankView(
                                        "帖子不存在",
                                        imageName: BlankImage.common
                                    )
                                )
                            }
                        )
                    )
                );
            }

            if (this._article.ownerType == "user") {
                if (this._article.userId != null &&
                    this.widget.viewModel.userDict.TryGetValue(key: this._article.userId, value: out this._user)) {
                    this._user = this.widget.viewModel.userDict[key: this._article.userId];
                }
            }

            if (this._article.ownerType == "team") {
                if (this._article.teamId != null &&
                    this.widget.viewModel.teamDict.TryGetValue(key: this._article.teamId, value: out this._team)) {
                    this._team = this.widget.viewModel.teamDict[key: this._article.teamId];
                }
            }

            if (this._titleHeight == 0f && this._article.title.isNotEmpty()) {
                this._titleHeight = CTextUtils.CalculateTextHeight(
                    text: this._article.title,
                    textStyle: CTextStyle.H3,
                    MediaQuery.of(context: context).size.width - 16 * 2 // 16 is horizontal padding
                ) + 16; // 16 is top padding
            }

            Widget contentWidget;

            if (this._article.bodyType == "markdown" && this._article.markdownPreviewBody.isNotEmpty()) {
                var contentHead = this._buildContentHead();
                var tagsWidget = this._buildTags();
                var relatedArticles = this._buildRelatedArticles();
                var comments = this._buildComments();

                contentWidget = new CustomMarkdown(
                    markdownStyleSheet: MarkdownUtils.defaultStyle(),
                    data: this._article.markdownPreviewBody,
                    syntaxHighlighter: new CSharpSyntaxHighlighter(),
                    onTapLink: url => this.widget.actionModel.openUrl(obj: url),
                    contentHead: contentHead,
                    tagsWidget: tagsWidget,
                    relatedArticles: relatedArticles,
                    commentList: comments,
                    refreshController: this._refreshController,
                    enablePullUp: this._article.hasMore, enablePullDown: false,
                    onRefresh: this._onRefresh,
                    onNotification: this._onNotification,
                    initialOffset: this._needRebuildWithCachedCommentPosition
                        ? this._cachedCommentPosition.Value
                        : 0f, needRebuildWithCachedCommentPosition: this._needRebuildWithCachedCommentPosition,
                    isArticleJumpToCommentStateActive: this._jumpState == _ArticleJumpToCommentState.active,
                    browserImageInMarkdown: url => {
                        Navigator.pushNamed(
                            context: context,
                            routeName: NavigatorRoutes.PhotoView,
                            new PhotoViewScreenArguments {
                                url = url,
                                urls = MarkdownUtils.markdownImages
                            }
                        );
                    },
                    videoSlices: this._article.videoSliceMap,
                    videoPosterMap: this._article.videoPosterMap,
                    playVideo: this.widget.actionModel.playVideo,
                    loginAction: this.pushToLoginPage,
                    nodes: this._article.markdownBodyNodes
                );
                this._jumpState = _ArticleJumpToCommentState.Inactive;
                if (this._needRebuildWithCachedCommentPosition) {
                    this._needRebuildWithCachedCommentPosition = false;
                    this._controller.forward();
                    this._isHaveTitle = true;
                }
            }
            else {
                var commentIndex = 0;
                var originItems = this._article == null
                    ? new List<Widget>()
                    : this._buildItems(context: context, commentIndex: out commentIndex);
                commentIndex = this._jumpState == _ArticleJumpToCommentState.active ? commentIndex : 0;
                this._jumpState = _ArticleJumpToCommentState.Inactive;

                //happens at the next frame after user presses the "Comment" button
                //we rebuild a CenteredRefresher so that we can calculate out the comment section's position
                if (this._needRebuildWithCachedCommentPosition == false && commentIndex != 0) {
                    contentWidget = new CenteredRefresher(
                        controller: this._refreshController,
                        enablePullDown: false,
                        enablePullUp: this._article.hasMore,
                        onRefresh: this._onRefresh,
                        onNotification: this._onNotification,
                        children: originItems,
                        centerIndex: commentIndex
                    );
                }
                else {
                    //happens when the page is updated or (when _needRebuildWithCachedCommentPosition is true) at the next frame after
                    //a CenteredRefresher is created and the comment section's position is estimated
                    //we use 0 or this estimated position to initiate the SmartRefresher's init scroll offset, respectively
                    D.assert(!this._needRebuildWithCachedCommentPosition || this._cachedCommentPosition != null);
                    contentWidget = new SmartRefresher(
                        initialOffset: this._needRebuildWithCachedCommentPosition
                            ? this._cachedCommentPosition.Value
                            : 0f,
                        controller: this._refreshController,
                        enablePullDown: false,
                        enablePullUp: this._article.hasMore,
                        onRefresh: this._onRefresh,
                        onNotification: this._onNotification,
                        child: ListView.builder(
                            physics: new AlwaysScrollableScrollPhysics(),
                            itemCount: originItems.Count,
                            itemBuilder: (cxt, index) => originItems[index: index]
                        ));
                    if (this._needRebuildWithCachedCommentPosition) {
                        this._needRebuildWithCachedCommentPosition = false;
                        //assume that when we jump to the comment, the title should always be shown as the header
                        //this assumption will fail when an article is shorter than 16 pixels in height (as referred to in _onNotification
                        this._controller.forward();
                        this._isHaveTitle = true;
                    }
                }
            }


            var notificationListener = new NotificationListener<UserScrollNotification>(
                child: contentWidget,
                onNotification: this._onUserNotification
            );
            var child = new Container(
                color: CColors.Background,
                child: new Column(
                    children: new List<Widget> {
                        this._buildNavigationBar(),
                        new Expanded(
                            child: new CrazyLikeButton(
                                new CustomScrollbar(
                                    child: notificationListener
                                ),
                                (this._article.like ?? false) && this.widget.viewModel.isLoggedIn,
                                this._article.appCurrentUserLikeCount ?? 0,
                                totalLikeCount: this._article.appLikeCount,
                                isPullUp: this._isPullUp,
                                () => {
                                    if (!this.widget.viewModel.isLoggedIn) {
                                        this.pushToLoginPage();
                                    }
                                    return this.widget.viewModel.isLoggedIn;
                                },
                                likeCount =>
                                    this.widget.actionModel.likeArticle(arg1: this._article.id, arg2: likeCount)
                            )
                        ),
                        this._buildArticleTabBar()
                    }
                )
            );
            return new Container(
                color: CColors.White,
                child: new CustomSafeArea(
                    child: child
                )
            );
        }

        List<Widget> _buildTagItems() {
            var widgets = new List<Widget>();
            this.widget.viewModel.tags.ForEach(item => {
                Widget tag = new GestureDetector(
                    onTap: () => {
                        Navigator.pushNamed(
                            context: this.context, 
                            routeName: NavigatorRoutes.Search,
                            new SearchScreenArguments {
                                searchType = SearchType.article,
                                keyword = item.name
                            }
                        );
                        AnalyticsManager.ClickArticleTag(articleId: this._article.id, tagId: item.id);
                    },
                    child: new Container(
                        decoration: new BoxDecoration(
                            color: CColors.TagBackground,
                            borderRadius: BorderRadius.all(14)
                        ),
                        height: 28,
                        padding: EdgeInsets.only(4, 3, 10, 3),
                        child: new Row(
                            mainAxisSize: MainAxisSize.min,
                            children: new List<Widget> {
                                new ClipRRect(
                                    borderRadius: BorderRadius.all(10),
                                    child: new Container(
                                        width: 20,
                                        height: 20,
                                        color: CColors.White,
                                        alignment: Alignment.center,
                                        child: new Icon(
                                            icon: CIcons.outline_tag,
                                            size: 16,
                                            color: CColors.PrimaryBlue
                                        )
                                    )
                                ),
                                new ConstrainedBox(
                                    constraints: new BoxConstraints(
                                        maxWidth: CCommonUtils.getTagTextMaxWidth(buildContext: this.context)
                                    ),
                                    child: new Container(
                                        padding: EdgeInsets.only(4),
                                        child: new Text(
                                            data: item.name,
                                            maxLines: 1,
                                            style: new TextStyle(
                                                fontSize: 14,
                                                fontFamily: "Roboto-Regular",
                                                color: CColors.PrimaryBlue
                                            ),
                                            overflow: TextOverflow.ellipsis
                                        )
                                    )
                                )
                            }
                        )
                    )
                );
                widgets.Add(item: tag);
            });
            return widgets;
        }


        Widget _buildTags() {
            if (this.widget.viewModel.tags.isNotNullAndEmpty()) {
                return new Container(
                    color: CColors.White,
                    padding: EdgeInsets.only(16, 0, 16, 24),
                    child: new Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: this._buildTagItems()
                    )
                );
            }

            return new Container();
        }

        List<Widget> _buildItems(BuildContext context, out int commentIndex) {
            var originItems = new List<Widget> {
                this._buildContentHead()
            };
            originItems.AddRange(
                ContentDescription.map(
                    context: context,
                    cont: this._article.body,
                    contentMap: this._article.contentMap,
                    videoSliceMap: this._article.videoSliceMap,
                    videoPosterMap: this._article.videoPosterMap,
                    openUrl: this.widget.actionModel.openUrl,
                    playVideo: this.widget.actionModel.playVideo,
                    loginAction: this.pushToLoginPage,
                    UserInfoManager.isLoggedIn()
                        ? CCommonUtils.GetUserLicense(
                            userId: UserInfoManager.getUserInfo().userId,
                            userLicenseMap: this.widget.viewModel.userLicenseDict)
                        : "",
                    url => {
                        Navigator.pushNamed(
                            context: context,
                            routeName: NavigatorRoutes.PhotoView,
                            new PhotoViewScreenArguments {
                                url = url,
                                urls = ContentDescription.imageUrls
                            }
                        );
                    },
                    detailContent: this._article.bodyDetailContent
                )
            );
            originItems.Add(this._buildTags());
            // originItems.Add(this._buildActionCards(this._article.like));
            originItems.Add(this._buildRelatedArticles());
            commentIndex = originItems.Count;
            originItems.AddRange(this._buildComments());

            return originItems;
        }

        Widget _buildNavigationBar(bool isShowRightWidget = true) {
            Widget titleWidget;
            if (this._isHaveTitle) {
                titleWidget = new Center(
                    child: new Text(
                        data: this._article.title,
                        style: CTextStyle.PXLargeMedium,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        textAlign: TextAlign.center
                    )
                );
            }
            else {
                titleWidget = new Container();
            }

            Widget rightWidget;
            if (isShowRightWidget) {
                var rightWidgetTitle = this._article.commentCount > 0
                    ? $"{this._article.commentCount} 评论"
                    : "抢个沙发";
                rightWidget = new Container(
                    margin: EdgeInsets.only(8, right: 16),
                    child: new CustomButton(
                        padding: EdgeInsets.zero,
                        onPressed: () => {
                            //do not jump if we are already at the exact comment position
                            if (this._refreshController.scrollController.position.pixels ==
                                this._cachedCommentPosition) {
                                return;
                            }

                            //first frame: create a new scroll view in which the center of the viewport is the comment widget
                            this.setState(
                                () => { this._jumpState = _ArticleJumpToCommentState.active; });

                            SchedulerBinding.instance.addPostFrameCallback((TimeSpan value2) => {
                                //calculate the comment position = curPixel(0) - minScrollExtent
                                var commentPosition = -this._refreshController.scrollController.position
                                    .minScrollExtent;

                                //cache the current comment position  
                                this._cachedCommentPosition = commentPosition;

                                //second frame: rebuild a smartRefresher with the cached _cacheCommmentPosition
                                this.setState(() => { this._needRebuildWithCachedCommentPosition = true; });
                            });
                        },
                        child: new Container(
                            height: 28,
                            padding: EdgeInsets.symmetric(horizontal: 16),
                            alignment: Alignment.center,
                            decoration: new BoxDecoration(
                                color: CColors.PrimaryBlue,
                                borderRadius: BorderRadius.all(14)
                            ),
                            child: new Text(
                                data: rightWidgetTitle,
                                style: new TextStyle(
                                    fontSize: 14,
                                    fontFamily: "Roboto-Medium",
                                    color: CColors.White
                                )
                            )
                        )
                    )
                );
            }
            else {
                rightWidget = new Container();
            }

            return new CustomAppBar(
                () => Navigator.pop(context: this.context),
                new Expanded(
                    child: new Stack(
                        fit: StackFit.expand,
                        children: new List<Widget> {
                            new PositionedTransition(
                                rect: this._animation,
                                child: titleWidget
                            )
                        }
                    )
                ),
                rightWidget: rightWidget,
                this._isHaveTitle ? CColors.Separator2 : CColors.Transparent
            );
        }

        Widget _buildArticleTabBar() {
            return new ArticleTabBar(
                (this._article.like ?? false) && this.widget.viewModel.isLoggedIn,
                this.widget.viewModel.isLoggedIn
                && this._article.favorites.isNotNullAndEmpty(),
                () => this._sendComment("Article"),
                () => this._sendComment("Article"),
                () => {
                    if (!this.widget.viewModel.isLoggedIn) {
                        this.pushToLoginPage();
                    }
                    else {
                        if (!(this._article.like ?? false)) {
                            this.widget.actionModel.likeArticle(arg1: this._article.id, 1);
                        }
                    }
                },
                () => {
                    if (!this.widget.viewModel.isLoggedIn) {
                        this.pushToLoginPage();
                    }
                    else {
                        ActionSheetUtils.showModalActionSheet(context: this.context,
                            new FavoriteSheetConnector(articleId: this._article.id));
                    }
                },
                shareCallback: this.share
            );
        }

        void _onRefresh(bool up) {
            if (!up) {
                this.widget.actionModel.fetchArticleComments(arg1: this._article.channelId,
                        arg2: this._article.currOldestMessageId)
                    .then(_ => { this._refreshController.sendBack(up: up, mode: RefreshStatus.idle); })
                    .catchError(err => { this._refreshController.sendBack(up: up, mode: RefreshStatus.failed); });
            }
        }

        void _onFollow(UserType userType, string userId) {
            if (this.widget.viewModel.isLoggedIn) {
                if (userType == UserType.follow) {
                    ActionSheetUtils.showModalActionSheet(
                        context: this.context,
                        new ActionSheet(
                            title: "确定不再关注？",
                            items: new List<ActionSheetItem> {
                                new ActionSheetItem("确定", type: ActionType.normal, () => {
                                    if (this._article.ownerType == OwnerType.user.ToString()) {
                                        this.widget.actionModel.startUnFollowUser(obj: userId);
                                        this.widget.actionModel.unFollowUser(arg: userId);
                                    }

                                    if (this._article.ownerType == OwnerType.team.ToString()) {
                                        this.widget.actionModel.startUnFollowTeam(obj: userId);
                                        this.widget.actionModel.unFollowTeam(arg: userId);
                                    }
                                }),
                                new ActionSheetItem("取消", type: ActionType.cancel)
                            }
                        )
                    );
                }

                if (userType == UserType.unFollow) {
                    if (this._article.ownerType == OwnerType.user.ToString()) {
                        this.widget.actionModel.startFollowUser(obj: userId);
                        this.widget.actionModel.followUser(arg: userId);
                    }

                    if (this._article.ownerType == OwnerType.team.ToString()) {
                        this.widget.actionModel.startFollowTeam(obj: userId);
                        this.widget.actionModel.followTeam(arg: userId);
                    }
                }
            }
            else {
                this.pushToLoginPage();
            }
        }

        bool _onNotification(ScrollNotification notification) {
            var axisDirection = notification.metrics.axisDirection;
            if (axisDirection == AxisDirection.left || axisDirection == AxisDirection.right) {
                return true;
            }

            var pixels = notification.metrics.pixels - notification.metrics.minScrollExtent;
            if (pixels > this._titleHeight) {
                if (this._isHaveTitle == false) {
                    this._controller.forward();
                    this.setState(() => this._isHaveTitle = true);
                }
            }
            else {
                if (this._isHaveTitle) {
                    this._controller.reverse();
                    this.setState(() => this._isHaveTitle = false);
                }
            }

            return true;
        }

        bool _onUserNotification(UserScrollNotification notification) {
            var axisDirection = notification.metrics.axisDirection;
            if (axisDirection == AxisDirection.left || axisDirection == AxisDirection.right) {
                return true;
            }

            if (notification.direction == ScrollDirection.reverse) {
                if (!this._isPullUp) {
                    this.setState(() => this._isPullUp = true);
                }
            }

            if (notification.direction == ScrollDirection.forward) {
                if (this._isPullUp) {
                    this.setState(() => this._isPullUp = false);
                }
            }

            return true;
        }

        Widget _buildContentHead() {
            Widget _avatar = this._article.ownerType == OwnerType.user.ToString()
                ? Avatar.User(user: this._user, 32)
                : Avatar.Team(team: this._team, 32);

            string name;
            Widget badge;
            string description;
            if (this._article.ownerType == "user") {
                // user
                name = this._user.fullName ?? this._user.name;
                badge = CImageUtils.GenBadgeImage(
                    badges: this._user.badges,
                    CCommonUtils.GetUserLicense(
                        userId: this._user.id,
                        userLicenseMap: this.widget.viewModel.userLicenseDict
                    ),
                    EdgeInsets.only(4)
                );
                description = this._user.title;
            }
            else {
                // team
                name = this._team.name;
                badge = CImageUtils.GenBadgeImage(
                    badges: this._team.badges,
                    CCommonUtils.GetUserLicense(
                        userId: this._team.id
                    ),
                    EdgeInsets.only(4)
                );
                description = this._team.description;
            }

            var time = this._article.publishedTime;
            Widget descriptionWidget;
            if (description.isNotEmpty()) {
                descriptionWidget = new Text(
                    data: description,
                    style: CTextStyle.PSmallBody3,
                    maxLines: 1
                );
            }
            else {
                descriptionWidget = new Container();
            }

            return new Container(
                color: CColors.White,
                padding: EdgeInsets.only(16, 16, 16),
                child: new Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: new List<Widget> {
                        new TipMenu(
                            new List<TipMenuItem> {
                                new TipMenuItem(
                                    "复制",
                                    () => Clipboard.setData(new ClipboardData(text: this._article.title))
                                )
                            },
                            new Container(
                                color: CColors.Transparent,
                                child: new Text(
                                    data: this._article.title,
                                    style: CTextStyle.H3
                                )
                            )
                        ),
                        new Container(
                            margin: EdgeInsets.only(top: 8),
                            child: new Text(
                                $"阅读 {this._article.viewCount} · {DateConvert.DateStringFromNow(dt: time)}",
                                style: CTextStyle.PSmallBody4
                            )
                        ),
                        new Row(
                            children: new List<Widget> {
                                new Expanded(
                                    child: new GestureDetector(
                                        onTap: () => {
                                            if (this._article.ownerType == OwnerType.user.ToString()) {
                                                Navigator.pushNamed(context: this.context, routeName: NavigatorRoutes.UserDetail,
                                                    new UserDetailScreenArguments {
                                                        id = this._user.id
                                                    }
                                                );
                                            }

                                            if (this._article.ownerType == OwnerType.team.ToString()) {
                                                Navigator.pushNamed(context: this.context, routeName: NavigatorRoutes.TeamDetail,
                                                    new TeamDetailScreenArguments {
                                                        id = this._team.id
                                                    }
                                                );
                                            }
                                        },
                                        child: new Container(
                                            margin: EdgeInsets.only(top: 24, right: 16, bottom: 24),
                                            color: CColors.Transparent,
                                            child: new Row(
                                                mainAxisSize: MainAxisSize.min,
                                                children: new List<Widget> {
                                                    new Container(
                                                        margin: EdgeInsets.only(right: 8),
                                                        child: _avatar
                                                    ),
                                                    new Expanded(
                                                        child: new Column(
                                                            mainAxisAlignment: MainAxisAlignment.center,
                                                            crossAxisAlignment: CrossAxisAlignment.start,
                                                            children: new List<Widget> {
                                                                new Row(
                                                                    children: new List<Widget> {
                                                                        new Flexible(
                                                                            child: new Text(
                                                                                data: name,
                                                                                style: CTextStyle.PRegularBody.merge(
                                                                                    new TextStyle(height: 1)),
                                                                                maxLines: 1,
                                                                                overflow: TextOverflow.ellipsis
                                                                            )
                                                                        ),
                                                                        badge
                                                                    }
                                                                ),
                                                                descriptionWidget
                                                            }
                                                        )
                                                    )
                                                }
                                            )
                                        )
                                    )
                                ),
                                this._buildFollowButton()
                            }
                        ),
                        this._article.subTitle.isEmpty()
                            ? new Container()
                            : (Widget) new TipMenu(
                                new List<TipMenuItem> {
                                    new TipMenuItem(
                                        "复制",
                                        () => Clipboard.setData(new ClipboardData(text: this._article.subTitle))
                                    )
                                },
                                new Container(
                                    margin: EdgeInsets.only(bottom: 24),
                                    decoration: new BoxDecoration(
                                        color: CColors.Separator2,
                                        borderRadius: BorderRadius.all(4)
                                    ),
                                    padding: EdgeInsets.only(16, 12, 16, 12),
                                    width: Screen.width - 32,
                                    child: new Text($"{this._article.subTitle}", style: CTextStyle.PLargeBody4)
                                )
                            )
                    }
                )
            );
        }

        Widget _buildFollowButton() {
            var id = this._article.ownerType == OwnerType.user.ToString() ? this._user.id : this._team.id;
            var userType = UserType.unFollow;
            if (!this.widget.viewModel.isLoggedIn) {
                userType = UserType.unFollow;
            }
            else {
                var followLoading = this._article.ownerType == OwnerType.user.ToString()
                    ? this._user.followUserLoading
                    : this._team.followTeamLoading;
                if (this.widget.viewModel.loginUserId == id) {
                    userType = UserType.me;
                }
                else if (followLoading ?? false) {
                    userType = UserType.loading;
                }
                else if (this.widget.viewModel.followMap.ContainsKey(key: id)) {
                    userType = UserType.follow;
                }
            }

            return new FollowButton(
                userType: userType,
                () => this._onFollow(userType: userType, userId: id)
            );
        }

        Widget _buildActionCards(bool like) {
            return new Container(
                color: CColors.White,
                padding: EdgeInsets.only(bottom: 40),
                child: new Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    crossAxisAlignment: CrossAxisAlignment.center,
                    children: new List<Widget> {
                        new ActionCard(like ? CIcons.favorite : CIcons.favorite_border, like ? "已赞" : "点赞", done: like,
                            () => {
                                if (!this.widget.viewModel.isLoggedIn) {
                                    this.pushToLoginPage();
                                }
                                else {
                                    if (!like) {
                                        this.widget.actionModel.likeArticle(arg1: this._article.id, 1);
                                    }
                                }
                            }),
                        new Container(width: 16),
                        new ActionCard(iconData: CIcons.share, "分享", false, onTap: this.share)
                    }
                )
            );
        }

        Widget _buildRelatedArticles() {
            if (this._article.projectIds == null || this._article.projectIds.Count == 0) {
                return new Container();
            }

            var widgets = new List<Widget>();
            this._article.projectIds.ForEach(articleId => {
                var article = this.widget.viewModel.articleDict[key: articleId];
                //对文章进行过滤
                if (article.id != this._article.id) {
                    string fullName;
                    if (article.ownerType == OwnerType.user.ToString()) {
                        fullName = this._user.fullName ?? this._user.name;
                    }
                    else if (article.ownerType == OwnerType.team.ToString()) {
                        fullName = this._team.name;
                    }
                    else {
                        fullName = "";
                    }

                    Widget card = new RelatedArticleCard(
                        article: article,
                        fullName: fullName,
                        () => {
                            Navigator.pushNamed(context: this.context, routeName: NavigatorRoutes.ArticleDetail,
                                new ArticleDetailScreenArguments {id = article.id});
                            AnalyticsManager.ClickEnterArticleDetail(
                                "ArticleDetail_Related",
                                articleId: article.id,
                                articleTitle: article.title
                            );
                        },
                        new ObjectKey(value: article.id)
                    );
                    widgets.Add(item: card);
                }
            });
            if (widgets.isNotEmpty()) {
                widgets.InsertRange(0, new List<Widget> {
                    new Container(
                        height: 1,
                        color: CColors.Separator2,
                        margin: EdgeInsets.only(16, 0, 16, 40)
                    ),
                    new Container(
                        margin: EdgeInsets.only(16, bottom: 16),
                        child: new Text(
                            "推荐阅读",
                            style: CTextStyle.H5
                        )
                    )
                });
            }

            return new Container(
                color: CColors.White,
                margin: EdgeInsets.only(bottom: 16),
                child: new Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: widgets
                )
            );
        }

        List<Widget> _buildComments() {
            var channelComments = new List<string>();
            if (this.widget.viewModel.channelMessageList.ContainsKey(key: this._article.channelId)) {
                channelComments = this.widget.viewModel.channelMessageList[key: this._article.channelId];
            }

            var mediaQuery = MediaQuery.of(context: this.context);
            var comments = new List<Widget> {
                new Container(
                    color: CColors.White,
                    width: mediaQuery.size.width,
                    padding: EdgeInsets.only(16, 16, 16),
                    child: new Text(
                        "评论",
                        style: CTextStyle.H5,
                        textAlign: TextAlign.left
                    )
                )
            };

            var titleHeight = CTextUtils.CalculateTextHeight(
                "评论",
                textStyle: CTextStyle.H5,
                mediaQuery.size.width - 16 * 2 // 16 is horizontal padding
            ) + 16; // 16 is top padding

            float safeAreaPadding = 0;
            if (Application.platform != RuntimePlatform.Android) {
                safeAreaPadding = mediaQuery.padding.vertical;
            }

            var height = mediaQuery.size.height - navBarHeight - 44 - safeAreaPadding;
            if (channelComments.Count == 0) {
                var blankView = new Container(
                    height: height - titleHeight,
                    child: new BlankView(
                        "快来写下第一条评论吧",
                        imageName: BlankImage.comment
                    )
                );
                comments.Add(item: blankView);
                return comments;
            }

            var messageDict = this.widget.viewModel.channelMessageDict[key: this._article.channelId];
            float contentHeights = 0;
            foreach (var commentId in channelComments) {
                if (!messageDict.ContainsKey(key: commentId)) {
                    break;
                }

                var message = messageDict[key: commentId];
                if (HistoryManager.isBlockUser(userId: message.author.id)) {
                    // is block user
                    continue;
                }

                var userLicense = CCommonUtils.GetUserLicense(userId: message.author.id,
                    userLicenseMap: this.widget.viewModel.userLicenseDict);
                var isPraised = _isPraised(message: message, loginUserId: this.widget.viewModel.loginUserId);
                var parentName = "";
                var parentAuthorId = "";
                if (message.upperMessageId.isNotEmpty()) {
                    if (messageDict.ContainsKey(key: message.upperMessageId)) {
                        var parentMessage = messageDict[key: message.upperMessageId];
                        parentName = parentMessage.author.fullName;
                        parentAuthorId = parentMessage.author.id;
                    }
                }
                else if (message.parentMessageId.isNotEmpty()) {
                    if (messageDict.ContainsKey(key: message.parentMessageId)) {
                        var parentMessage = messageDict[key: message.parentMessageId];
                        parentName = parentMessage.author.fullName;
                        parentAuthorId = parentMessage.author.id;
                    }
                }

                var content = MessageUtils.AnalyzeMessage(content: message.content, mentions: message.mentions,
                    mentionEveryone: message.mentionEveryone) + (parentName.isEmpty() ? "" : $"回复@{parentName}");
                var contentHeight = CTextUtils.CalculateTextHeight(
                    text: content,
                    textStyle: CTextStyle.PLargeBody,
                    // 16 is horizontal padding, 24 is avatar size, 8 is content left margin to avatar
                    mediaQuery.size.width - 16 * 2 - 24 - 8
                ) + 16 + 24 + 3 + 5 + 22 + 12;
                // 16 is top padding, 24 is avatar size, 3 is content top margin to avatar, 5 is content bottom margin to commentTime
                // 22 is commentTime height, 12 is commentTime bottom margin
                contentHeights += contentHeight;
                var card = new ArticleCommentCard(
                    message: message,
                    userLicense: userLicense,
                    isPraised: isPraised,
                    parentName: parentName,
                    parentAuthorId: parentAuthorId,
                    () => ReportManager.showReportView(context: this.context,
                        isLoggedIn: this.widget.viewModel.isLoggedIn,
                        userName: message.author.fullName,
                        reportType: ReportType.comment,
                        () => this.pushToLoginPage(),
                        () => Navigator.pushNamed(
                            context: this.context,
                            routeName: NavigatorRoutes.Report,
                            new ReportScreenArguments {
                                id = commentId,
                                reportType = ReportType.comment
                            }
                        ),
                        blockUserCallback: () => this.widget.actionModel.blockUserAction(obj: message.author.id)
                    ),
                    replyCallBack: () => this._sendComment(
                        "Article_Comment",
                        message.parentMessageId.isNotEmpty() ? message.parentMessageId : commentId,
                        message.parentMessageId.isNotEmpty() ? commentId : "",
                        message.author.fullName.isEmpty() ? "" : message.author.fullName
                    ),
                    praiseCallBack: () => {
                        if (!this.widget.viewModel.isLoggedIn) {
                            this.pushToLoginPage();
                        }
                        else {
                            if (isPraised) {
                                this.widget.actionModel.removeLikeComment(arg: message);
                            }
                            else {
                                this.widget.actionModel.likeComment(arg: message);
                            }
                        }
                    },
                    pushToUserDetail: userId => {
                        Navigator.pushNamed(context: this.context, routeName: NavigatorRoutes.UserDetail,
                            new UserDetailScreenArguments {
                                id = userId
                            }
                        );
                    });
                comments.Add(item: card);
            }

            // fix when only has one comment, blocked it show empty view 
            if (comments.Count == 1) {
                var blankView = new Container(
                    height: height - titleHeight,
                    child: new BlankView(
                        "快来写下第一条评论吧",
                        imageName: BlankImage.comment
                    )
                );
                comments.Add(item: blankView);
                return comments;
            }

            float endHeight = 0;
            if (!this._article.hasMore) {
                comments.Add(new EndView());
                endHeight = 52;
            }

            if (titleHeight + contentHeights + endHeight < height) {
                return new List<Widget> {
                    new Container(
                        height: height,
                        child: new Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: comments
                        )
                    )
                };
            }

            return comments;
        }

        static bool _isPraised(Message message, string loginUserId) {
            foreach (var reaction in message.reactions) {
                if (reaction.user.id == loginUserId) {
                    return true;
                }
            }

            return false;
        }

        void share() {
            var userId = "";
            if (this._article.ownerType == OwnerType.user.ToString()) {
                userId = this._article.userId;
            }

            if (this._article.ownerType == OwnerType.team.ToString()) {
                userId = this._article.teamId;
            }

            var linkUrl = CStringUtils.JointProjectShareLink(projectId: this._article.id);

            ShareManager.showDoubleDeckShareView(context: this.context,
                this.widget.viewModel.loginUserId != userId,
                isLoggedIn: this.widget.viewModel.isLoggedIn,
                () => {
                    Clipboard.setData(new ClipboardData(text: linkUrl));
                    CustomDialogUtils.showToast(context: this.context, "复制链接成功", iconData: CIcons.check_circle_outline);
                },
                () => this.pushToLoginPage(),
                () => this.widget.actionModel.blockArticleAction(obj: this._article.id),
                () => Navigator.pushNamed(
                    context: this.context,
                    routeName: NavigatorRoutes.Report,
                    new ReportScreenArguments {
                        id = this._article.id,
                        reportType = ReportType.article
                    }
                ),
                type => {
                    CustomDialogUtils.showCustomDialog(
                        context: this.context,
                        child: new CustomLoadingDialog()
                    );
                    var imageUrl = CImageUtils.SizeTo400ImageUrl(imageUrl: this._article.thumbnail.url);
                    this.widget.actionModel.shareToWechat(
                            arg1: type,
                            arg2: this._article.title,
                            arg3: this._article.subTitle,
                            arg4: linkUrl,
                            arg5: imageUrl,
                            ""
                        ).then(_ => CustomDialogUtils.hiddenCustomDialog(context: this.context))
                        .catchError(_ => CustomDialogUtils.hiddenCustomDialog(context: this.context));
                },
                mainRouterPop: () => Navigator.pop(context: this.context)
            );
        }

        void _sendComment(string type, string parentMessageId = "", string upperMessageId = "",
            string replyUserName = null) {
            if (!this.widget.viewModel.isLoggedIn) {
                this.pushToLoginPage();
            }
            else if (!UserInfoManager.isRealName()) {
                Navigator.pushNamed(
                    context: this.context,
                    routeName: NavigatorRoutes.RealName
                );
            }
            else {
                AnalyticsManager.ClickComment(
                    type: type,
                    channelId: this._article.channelId,
                    title: this._article.title,
                    commentId: parentMessageId
                );
                ActionSheetUtils.showModalActionSheet(
                    context: this.context,
                    new CustomInput(
                        replyUserName: replyUserName,
                        text => {
                            ActionSheetUtils.hiddenModalPopup(context: this.context);
                            this.widget.actionModel.sendComment(
                                arg1: this._article.channelId,
                                arg2: text,
                                Snowflake.CreateNonce(),
                                arg4: parentMessageId,
                                arg5: upperMessageId
                            ).then(_ => {
                                CustomDialogUtils.showToast(context: this.context, "评论成功，会在审核通过后展示",
                                    iconData: CIcons.sentiment_satisfied, 2);
                            }).catchError(_ => {
                                CustomDialogUtils.showToast(context: this.context, "评论失败",
                                    iconData: CIcons.sentiment_dissatisfied, 2);
                            });
                        })
                );
            }
        }

        public void didPopNext() {
            if (this.widget.viewModel.articleId.isNotEmpty()) {
                CTemporaryValue.currentPageModelId = this.widget.viewModel.articleId;
            }

            StatusBarManager.statusBarStyle(false);
        }

        public void didPush() {
            if (this.widget.viewModel.articleId.isNotEmpty()) {
                CTemporaryValue.currentPageModelId = this.widget.viewModel.articleId;
            }
        }

        public void didPop() {
            if (CTemporaryValue.currentPageModelId.isNotEmpty() &&
                this.widget.viewModel.articleId == CTemporaryValue.currentPageModelId) {
                CTemporaryValue.currentPageModelId = null;
            }
        }

        public void didPushNext() {
            CTemporaryValue.currentPageModelId = null;
        }
    }
}